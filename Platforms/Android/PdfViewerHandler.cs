#if ANDROID
using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Handlers;
using PAGELY.Controls;
using PAGELY.Services;

namespace PAGELY.Platforms.Android;

public class PdfViewerHandler : ViewHandler<PdfViewer, RecyclerView>
{
    public static readonly IPropertyMapper<PdfViewer, PdfViewerHandler> Mapper =
        new PropertyMapper<PdfViewer, PdfViewerHandler>(ViewMapper)
        {
            [nameof(PdfViewer.PagePaths)] = MapPagePaths,
            [nameof(PdfViewer.SourcePath)] = MapSourcePath,
            [nameof(PdfViewer.RenderWidth)] = MapRenderWidth,
            [nameof(PdfViewer.CurrentPageIndex)] = MapCurrentPageIndex,
        };

    public static readonly CommandMapper<PdfViewer, PdfViewerHandler> CommandMap =
        new(ViewCommandMapper)
        {
            [nameof(PdfViewer.ScrollToPage)] = MapScrollToPage
        };

    private PdfPageAdapter? _adapter;
    private LinearLayoutManager? _layoutManager;
    private PdfScrollListener? _scrollListener;

    public PdfViewerHandler() : base(Mapper, CommandMap)
    {
    }

    protected override RecyclerView CreatePlatformView()
    {
        var context = Context;
        var recyclerView = new RecyclerView(context)
        {
            HasFixedSize = false
        };

        // Dark background matching reader theme
        recyclerView.SetBackgroundColor(global::Android.Graphics.Color.Rgb(0x1A, 0x1A, 0x1A));

        _layoutManager = new LinearLayoutManager(context, LinearLayoutManager.Vertical, false);
        recyclerView.SetLayoutManager(_layoutManager);

        _adapter = new PdfPageAdapter(context, ResolvePdfRenderer());
        _adapter.SetSource(VirtualView?.SourcePath);
        _adapter.SetRenderWidth(VirtualView?.RenderWidth ?? 0);
        recyclerView.SetAdapter(_adapter);

        _scrollListener = new PdfScrollListener(this);
        recyclerView.AddOnScrollListener(_scrollListener);

        // Keep the view cache bounded to minimize memory
        recyclerView.SetItemViewCacheSize(2);

        return recyclerView;
    }

    /// <summary>
    /// The PDF renderer comes from the app's service provider, exactly like the
    /// view model that owns the page list does.
    /// </summary>
    private IPdfRendererService? ResolvePdfRenderer()
    {
        try
        {
            var services = MauiContext?.Services ?? PAGELY.MauiProgram.Services;
            return services?.GetService<IPdfRendererService>();
        }
        catch
        {
            return null;
        }
    }

    protected override void DisconnectHandler(RecyclerView platformView)
    {
        if (_scrollListener != null)
        {
            platformView.RemoveOnScrollListener(_scrollListener);
            _scrollListener = null;
        }

        platformView.SetAdapter(null);
        _adapter?.Dispose();
        _adapter = null;
        _layoutManager = null;

        base.DisconnectHandler(platformView);
    }

    public static void MapPagePaths(PdfViewerHandler handler, PdfViewer virtualView)
    {
        if (handler._adapter is null)
        {
            return;
        }

        handler._adapter.SetPages(virtualView.PagePaths);

        if (virtualView.CurrentPageIndex > 0 && handler._layoutManager is not null)
        {
            handler._layoutManager.ScrollToPositionWithOffset(virtualView.CurrentPageIndex, 0);
        }
    }

    public static void MapSourcePath(PdfViewerHandler handler, PdfViewer virtualView)
    {
        handler._adapter?.SetSource(virtualView.SourcePath);
    }

    /// <summary>
    /// The rasterisation width is decided by the view model, which stamps the same
    /// value onto the page files, so the adapter only ever follows it.
    /// </summary>
    public static void MapRenderWidth(PdfViewerHandler handler, PdfViewer virtualView)
    {
        handler._adapter?.SetRenderWidth(virtualView.RenderWidth);
    }

    public static void MapCurrentPageIndex(PdfViewerHandler handler, PdfViewer virtualView)
    {
        // Property binding sync; programmatic scrolling goes through ScrollToPage so
        // that user scrolling is never fought by the binding.
    }

    public static void MapScrollToPage(PdfViewerHandler handler, PdfViewer virtualView, object? arg)
    {
        if (arg is int pageIndex && handler._layoutManager is not null)
        {
            handler._layoutManager.ScrollToPositionWithOffset(pageIndex, 0);
        }
    }

    internal void OnScrolled()
    {
        if (_layoutManager == null || VirtualView == null || _adapter == null || _adapter.ItemCount == 0)
        {
            return;
        }

        int firstVisible = _layoutManager.FindFirstVisibleItemPosition();
        if (firstVisible >= 0 && firstVisible < _adapter.ItemCount)
        {
            int total = _adapter.ItemCount;
            double percent = ((firstVisible + 1.0) / total) * 100.0;
            VirtualView.NotifyPageScrolled(firstVisible, total, percent);
        }
    }
}

/// <summary>
/// RecyclerView adapter for PDF pages.
///
/// Only a handful of views exist at any time (what is on screen plus a small
/// view cache), so pages are rasterised lazily: a page that has not been rendered
/// yet is produced on a background thread the moment it is about to be shown, and
/// its neighbours are warmed at the same time. Nothing is ever sized by the
/// length of the document.
/// </summary>
internal class PdfPageAdapter : RecyclerView.Adapter
{
    /// <summary>Decoded pages kept in RAM: roughly 9 pages of a 1080px screen at RGB_565.</summary>
    private const long BitmapCacheBytes = 32L * 1024 * 1024;

    /// <summary>Rasterising is expensive; two at a time keeps the UI responsive.</summary>
    private const int MaxConcurrentRenders = 2;

    private readonly Context _context;
    private readonly PageBitmapCache _bitmaps = new(BitmapCacheBytes);
    private readonly SemaphoreSlim _renderGate = new(MaxConcurrentRenders);
    private readonly CancellationTokenSource _lifetime = new();

    private IReadOnlyList<string> _pages = Array.Empty<string>();
    private IPdfRendererService? _renderer;
    private string? _sourcePath;
    private int _itemWidth;
    private int _itemHeight;
    private int _renderWidth;
    private int _requestedRenderWidth;
    private bool _disposed;

    public PdfPageAdapter(Context context, IPdfRendererService? renderer)
    {
        _context = context;
        _renderer = renderer;
        CalculateItemDimensions();
    }

    public void SetSource(string? sourcePath)
    {
        _sourcePath = sourcePath;
    }

    /// <summary>
    /// Width the owner wants pages rasterised at, so the JPEG written to disk is the
    /// resolution the viewer decodes. <c>0</c> leaves the choice to the screen size.
    /// </summary>
    public void SetRenderWidth(int renderWidth)
    {
        if (renderWidth == _requestedRenderWidth)
        {
            return;
        }

        _requestedRenderWidth = renderWidth;
        CalculateItemDimensions();
    }

    public void SetPages(IReadOnlyList<string>? pages)
    {
        _pages = pages ?? Array.Empty<string>();
        _bitmaps.Clear();
        CalculateItemDimensions();
        NotifyDataSetChanged();
    }

    public override int ItemCount => _pages.Count;

    private void CalculateItemDimensions()
    {
        var metrics = _context.Resources?.DisplayMetrics;
        int screenWidth = metrics?.WidthPixels ?? 1080;
        int screenHeight = metrics?.HeightPixels ?? 1920;
        _itemWidth = screenWidth;
        _itemHeight = (int)(_itemWidth * 1.414);

        // Pages are rasterised at the resolution they will be shown at: the old fixed
        // 1200px ceiling meant a wider screen upscaled every page. PdfPageFiles owns the
        // policy (and the reader stamps the same number onto the page files); the owner's
        // value wins so the file on disk and the decode below can never drift apart. The
        // fallback uses the shorter side, which is what the reader passes, so rotation
        // does not change the answer.
        _renderWidth = _requestedRenderWidth > 0
            ? _requestedRenderWidth
            : PdfPageFiles.ComputeRenderWidth(Math.Min(screenWidth, screenHeight));
    }

    public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
    {
        if (parent.Width > 0 && parent.Width != _itemWidth)
        {
            _itemWidth = parent.Width;
            _itemHeight = (int)(_itemWidth * 1.414);
        }

        // Explicit item height (never WrapContent) so the scroll range is known
        // before a single page image has been decoded.
        var container = new FrameLayout(_context);
        var layoutParams = new RecyclerView.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            _itemHeight);

        int marginPx = (int)(8 * (_context.Resources?.DisplayMetrics?.Density ?? 1.0f));
        layoutParams.SetMargins(0, 0, 0, marginPx);
        container.LayoutParameters = layoutParams;
        container.SetBackgroundColor(global::Android.Graphics.Color.White);

        var imageView = new ImageView(_context)
        {
            LayoutParameters = new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.MatchParent)
        };
        imageView.SetScaleType(ImageView.ScaleType.FitCenter);
        container.AddView(imageView);

        var placeholder = new TextView(_context)
        {
            LayoutParameters = new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.MatchParent)
        };
        placeholder.SetTextColor(global::Android.Graphics.Color.Rgb(0x99, 0x99, 0x99));
        placeholder.SetTextSize(global::Android.Util.ComplexUnitType.Sp, 14f);
        placeholder.Gravity = GravityFlags.Center;
        placeholder.Text = "Rendering page…";
        container.AddView(placeholder);

        return new PdfPageViewHolder(container, imageView, placeholder);
    }

    public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
    {
        if (holder is not PdfPageViewHolder pageHolder)
        {
            return;
        }

        ResetHolder(pageHolder);

        if (position < 0 || position >= _pages.Count)
        {
            return;
        }

        var pagePath = _pages[position];
        if (string.IsNullOrEmpty(pagePath))
        {
            return;
        }

        pageHolder.BoundPath = pagePath;

        var cached = _bitmaps.Get(pagePath);
        if (cached is not null)
        {
            AttachBitmap(pageHolder, cached);
        }
        else
        {
            var cts = new CancellationTokenSource();
            pageHolder.Cts = cts;
            _ = BindPageAsync(pageHolder, position, pagePath, cts.Token);
        }

        Prefetch(position);
    }

    /// <summary>Drops everything the holder was showing so it can be reused.</summary>
    private void ResetHolder(PdfPageViewHolder holder)
    {
        // Cancel only: an in flight render may still be inspecting the token, and
        // registering on a disposed source throws.
        holder.Cts?.Cancel();
        holder.Cts = null;

        if (holder.BoundBitmap is { } bound)
        {
            _bitmaps.Release(bound);
            holder.BoundBitmap = null;
        }

        holder.BoundPath = null;
        holder.ImageView.SetImageDrawable(null);
        holder.ShowPlaceholder();
    }

    private async Task BindPageAsync(PdfPageViewHolder holder, int position, string pagePath, CancellationToken ct)
    {
        try
        {
            var bitmap = await LoadBitmapAsync(position, pagePath, ct).ConfigureAwait(false);
            if (bitmap is null)
            {
                return;
            }

            // Views may only be touched from the UI thread.
            holder.Container.Post(() =>
            {
                if (_disposed || ct.IsCancellationRequested || holder.BoundPath != pagePath)
                {
                    return;
                }

                AttachBitmap(holder, bitmap);
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PDF VIEWER: page {position} failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void AttachBitmap(PdfPageViewHolder holder, Bitmap bitmap)
    {
        // One reference per holder. A holder can be handed a different bitmap without an
        // intervening ResetHolder (rebind while a decode is in flight), and a reference
        // left behind would pin the old bitmap for as long as the cache lives.
        if (holder.BoundBitmap is { } previous && !ReferenceEquals(previous, bitmap))
        {
            _bitmaps.Release(previous);
        }

        _bitmaps.AddRef(bitmap);
        holder.BoundBitmap = bitmap;
        holder.ImageView.SetImageBitmap(bitmap);
        holder.HidePlaceholder();
    }

    private async Task<Bitmap?> LoadBitmapAsync(int position, string pagePath, CancellationToken ct)
    {
        var cached = _bitmaps.Get(pagePath);
        if (cached is not null)
        {
            return cached;
        }

        if (!File.Exists(pagePath) && !await EnsurePageRenderedAsync(position, pagePath, ct).ConfigureAwait(false))
        {
            return null;
        }

        if (ct.IsCancellationRequested)
        {
            return null;
        }

        var bitmap = await Task.Run(() => DecodePage(pagePath), ct).ConfigureAwait(false);
        if (bitmap is null)
        {
            return null;
        }

        if (ct.IsCancellationRequested)
        {
            Recycle(bitmap);
            return null;
        }

        return _bitmaps.Store(pagePath, bitmap);
    }

    /// <summary>
    /// Rasterises a page to disk when it is not there yet. Returns false when the
    /// page cannot be produced (no source, unsupported document, cancellation).
    /// </summary>
    private async Task<bool> EnsurePageRenderedAsync(int position, string pagePath, CancellationToken ct)
    {
        var renderer = _renderer;
        var sourcePath = _sourcePath;

        if (renderer is null || string.IsNullOrEmpty(sourcePath))
        {
            return false;
        }

        try
        {
            await _renderGate.WaitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }

        try
        {
            if (File.Exists(pagePath))
            {
                return true;
            }

            if (ct.IsCancellationRequested)
            {
                return false;
            }

            return await renderer
                .RenderPageToFileAsync(sourcePath, position, pagePath, _renderWidth, ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PDF VIEWER: rendering page {position} failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
        finally
        {
            _renderGate.Release();
        }
    }

    /// <summary>
    /// Warms the neighbouring pages. Rendered pages are reused from disk, so this
    /// is idempotent and never repeats work.
    /// </summary>
    private void Prefetch(int position)
    {
        if (_disposed || _renderer is null || _sourcePath is null)
        {
            return;
        }

        PrefetchPage(position + 1);
        PrefetchPage(position - 1);
    }

    private void PrefetchPage(int position)
    {
        if (position < 0 || position >= _pages.Count)
        {
            return;
        }

        var pagePath = _pages[position];
        if (string.IsNullOrEmpty(pagePath) || File.Exists(pagePath))
        {
            return;
        }

        _ = EnsurePageRenderedAsync(position, pagePath, _lifetime.Token);
    }

    /// <summary>
    /// Decodes a page image, downsampled to the width of the view that will show
    /// it and in RGB_565 so a page costs 2 bytes per pixel instead of 4.
    /// </summary>
    private Bitmap? DecodePage(string pagePath)
    {
        try
        {
            var targetWidth = Math.Max(1, _itemWidth);

            var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
            BitmapFactory.DecodeFile(pagePath, bounds);

            var sampleSize = 1;
            if (bounds.OutWidth > 0)
            {
                while (bounds.OutWidth / (sampleSize * 2) >= targetWidth)
                {
                    sampleSize *= 2;
                }
            }

            var options = new BitmapFactory.Options
            {
                InSampleSize = sampleSize,
                InPreferredConfig = Bitmap.Config.Rgb565
            };

            return BitmapFactory.DecodeFile(pagePath, options);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PDF VIEWER: decoding {pagePath} failed: {ex.GetType().Name}: {ex.Message}");

            // Almost certainly out of memory; what the cache holds back is the only
            // part of the heap we control, so give it up.
            _bitmaps.Clear();
            return null;
        }
    }

    public override void OnViewRecycled(Java.Lang.Object holder)
    {
        base.OnViewRecycled(holder);

        if (holder is PdfPageViewHolder pageHolder)
        {
            ResetHolder(pageHolder);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;

            // Cancel only: a disposed token source could throw from an in flight render.
            try
            {
                _lifetime.Cancel();
            }
            catch
            {
            }

            _bitmaps.Clear();
        }

        base.Dispose(disposing);
    }

    private static void Recycle(Bitmap bitmap)
    {
        try
        {
            if (!bitmap.IsRecycled)
            {
                bitmap.Recycle();
            }
        }
        catch
        {
        }
    }
}

internal class PdfPageViewHolder : RecyclerView.ViewHolder
{
    public FrameLayout Container { get; }
    public ImageView ImageView { get; }
    public TextView Placeholder { get; }
    public string? BoundPath { get; set; }
    public Bitmap? BoundBitmap { get; set; }
    public CancellationTokenSource? Cts { get; set; }

    public PdfPageViewHolder(FrameLayout container, ImageView imageView, TextView placeholder)
        : base(container)
    {
        Container = container;
        ImageView = imageView;
        Placeholder = placeholder;
    }

    public void ShowPlaceholder() => Placeholder.Visibility = ViewStates.Visible;

    public void HidePlaceholder() => Placeholder.Visibility = ViewStates.Gone;
}

/// <summary>
/// Byte budgeted LRU cache of decoded page bitmaps.
///
/// A bitmap that is currently shown by a view is never recycled, so scrolling can
/// never draw from freed pixels; it is released as soon as the last view showing
/// it lets go. The budget is what keeps native bitmap memory flat no matter how
/// long the document is.
/// </summary>
internal sealed class PageBitmapCache
{
    private readonly object _gate = new();
    private readonly Dictionary<string, LinkedListNode<Entry>> _entries = new(StringComparer.Ordinal);
    private readonly LinkedList<Entry> _lru = new();
    private readonly Dictionary<Bitmap, int> _inUse = new();
    private readonly HashSet<Bitmap> _detached = new();
    private readonly long _maxBytes;
    private long _bytes;

    public PageBitmapCache(long maxBytes) => _maxBytes = maxBytes;

    private sealed class Entry
    {
        public Entry(string key, Bitmap bitmap)
        {
            Key = key;
            Bitmap = bitmap;
        }

        public string Key { get; }

        public Bitmap Bitmap { get; }
    }

    public Bitmap? Get(string key)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out var node))
            {
                return null;
            }

            if (node.Value.Bitmap.IsRecycled)
            {
                Remove(node, recycle: false);
                return null;
            }

            _lru.Remove(node);
            _lru.AddFirst(node);
            return node.Value.Bitmap;
        }
    }

    /// <summary>Adds a bitmap and returns the instance that stayed in the cache.</summary>
    public Bitmap? Store(string key, Bitmap bitmap)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var existing))
            {
                if (ReferenceEquals(existing.Value.Bitmap, bitmap))
                {
                    return bitmap;
                }

                Remove(existing, recycle: !_inUse.ContainsKey(existing.Value.Bitmap));
            }

            var node = new LinkedListNode<Entry>(new Entry(key, bitmap));
            _lru.AddFirst(node);
            _entries[key] = node;
            _bytes += bitmap.ByteCount;

            Trim();

            return bitmap.IsRecycled ? null : bitmap;
        }
    }

    /// <summary>Marks a bitmap as being displayed by a view.</summary>
    public void AddRef(Bitmap bitmap)
    {
        lock (_gate)
        {
            _inUse.TryGetValue(bitmap, out var count);
            _inUse[bitmap] = count + 1;
        }
    }

    /// <summary>Releases a bitmap that no view is showing anymore.</summary>
    public void Release(Bitmap bitmap)
    {
        lock (_gate)
        {
            if (!_inUse.TryGetValue(bitmap, out var count))
            {
                return;
            }

            if (count > 1)
            {
                _inUse[bitmap] = count - 1;
                return;
            }

            _inUse.Remove(bitmap);

            // Evicted while it was on screen: its native memory can go now.
            if (_detached.Remove(bitmap))
            {
                Recycle(bitmap);
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            var nodes = new List<LinkedListNode<Entry>>(_lru.Count);
            for (var node = _lru.First; node is not null; node = node.Next)
            {
                nodes.Add(node);
            }

            _lru.Clear();
            _entries.Clear();
            _bytes = 0;

            foreach (var node in nodes)
            {
                if (_inUse.ContainsKey(node.Value.Bitmap))
                {
                    _detached.Add(node.Value.Bitmap);
                }
                else
                {
                    Recycle(node.Value.Bitmap);
                }
            }
        }
    }

    private void Trim()
    {
        // Never evict the most recently used entry: that is the bitmap the caller is
        // about to attach, and recycling it before it reaches a view would leave the
        // page stuck on its placeholder (Store returns null for an evicted bitmap).
        // One entry may therefore sit above the byte budget, which is harmless.
        var node = _lru.Last;

        while (_bytes > _maxBytes && node is not null && node != _lru.First)
        {
            // Grab the neighbour first: removing a node clears its links.
            var previous = node.Previous;

            if (!_inUse.ContainsKey(node.Value.Bitmap))
            {
                Remove(node, recycle: true);
            }

            node = previous;
        }
    }

    private void Remove(LinkedListNode<Entry> node, bool recycle)
    {
        _entries.Remove(node.Value.Key);
        _lru.Remove(node);
        _bytes -= node.Value.Bitmap.ByteCount;

        if (recycle)
        {
            Recycle(node.Value.Bitmap);
        }
        else
        {
            _detached.Add(node.Value.Bitmap);
        }
    }

    private static void Recycle(Bitmap bitmap)
    {
        try
        {
            if (!bitmap.IsRecycled)
            {
                bitmap.Recycle();
            }
        }
        catch
        {
        }
    }
}

internal class PdfScrollListener : RecyclerView.OnScrollListener
{
    private readonly WeakReference<PdfViewerHandler> _handlerRef;

    public PdfScrollListener(PdfViewerHandler handler)
    {
        _handlerRef = new WeakReference<PdfViewerHandler>(handler);
    }

    public override void OnScrolled(RecyclerView recyclerView, int dx, int dy)
    {
        base.OnScrolled(recyclerView, dx, dy);
        if (_handlerRef.TryGetTarget(out var handler))
        {
            handler.OnScrolled();
        }
    }
}
#endif
