using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PAGELY.Data;
using PAGELY.Models;
using PAGELY.Services;

namespace PAGELY.ViewModels;

public partial class ReaderViewModel : ObservableObject, IDisposable
{
    private readonly DatabaseService _database;
    private readonly BookContentService _content;
    private readonly IPdfRendererService? _pdfRenderer;
    private List<string> _chapters = [];
    private bool _disposed;

    public ReaderViewModel(DatabaseService database, BookContentService content)
    {
        _database = database;
        _content = content;
    }

    public ReaderViewModel(DatabaseService database, BookContentService content, IPdfRendererService pdfRenderer)
    {
        _database = database;
        _content = content;
        _pdfRenderer = pdfRenderer;
    }

    [ObservableProperty]
    private Book? book;

    [ObservableProperty]
    private HtmlWebViewSource pageSource = new() { Html = BookContentService.WrapHtml("<p>Opening book…</p>") };

    [ObservableProperty]
    private string header = "Reader";

    [ObservableProperty]
    private string progressLabel = "";

    [ObservableProperty]
    private bool canGoPrevious;

    [ObservableProperty]
    private bool canGoNext;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isPdfMode;

    [ObservableProperty]
    private List<string> pdfPagePaths = [];

    /// <summary>Source PDF whose pages are rasterised on demand by the native viewer.</summary>
    [ObservableProperty]
    private string? pdfSourcePath;

    /// <summary>
    /// Width in pixels pages are rasterised at, from <see cref="PdfPageFiles.ComputeRenderWidth"/>.
    /// The native viewer renders at exactly this width.
    /// </summary>
    [ObservableProperty]
    private int renderWidth;

    [ObservableProperty]
    private int currentPdfPage;

    public int PendingScrollPercent { get; private set; }

    [RelayCommand]
    public async Task LoadAsync(int id)
    {
        try
        {
            IsBusy = true;
            Book = await _database.GetBookAsync(id);
            if (Book is null)
            {
                PageSource = new HtmlWebViewSource { Html = BookContentService.WrapHtml("<p>This book could not be found.</p>") };
                return;
            }

            _chapters = await _content.LoadChaptersAsync(Book);
            Book.TotalChapters = Math.Max(1, _chapters.Count);
            if (Book.CurrentChapterIndex >= _chapters.Count)
            {
                Book.CurrentChapterIndex = Math.Max(0, _chapters.Count - 1);
            }

            if (Book.ReadingStatus == ReadingStatus.ToBeRead)
            {
                Book.ReadingStatus = ReadingStatus.CurrentlyReading;
            }

            var isPdf = string.Equals(Book.FileType, "pdf", StringComparison.OrdinalIgnoreCase);
            if (isPdf)
            {
                IsPdfMode = true;
                Header = Book.Title;

                var pagesDirectory = EnsurePdfPagesDirectory(Book);

                // Decide the page resolution before anything is rendered: it is handed to
                // the native viewer, so the JPEG on disk and the bitmap on screen are
                // never at different resolutions.
                RenderWidth = PdfPageFiles.ComputeRenderWidth(GetShorterDisplaySidePixels());

                // Count first, then drop pages left by another width: the count falls back
                // to the pages already on disk when the document cannot be opened, and
                // that fallback is only worth having while those pages are still there.
                var pageCount = await ResolvePdfPageCountAsync(Book.FilePath, pagesDirectory);
                await PreparePageDirectoryAsync(pagesDirectory, RenderWidth);

                // Tracked so the folder is cleaned up even if the document cannot be opened.
                Book.RenderedPagesDir = pagesDirectory;

                if (pageCount > 0)
                {
                    // Only the page count is needed here: each page image is rasterised
                    // by the native viewer the first time it scrolls into view, so
                    // opening a document never loads the whole thing into memory.
                    Book.TotalChapters = pageCount;
                    PdfSourcePath = Book.FilePath;
                    CanGoPrevious = false;
                    CanGoNext = false;

                    var savedPage = Math.Clamp(Book.CurrentChapterIndex, 0, pageCount - 1);
                    Book.CurrentChapterIndex = savedPage;

                    // Restore the position before the page list is published so the
                    // native viewer can jump straight to it.
                    CurrentPdfPage = savedPage;
                    PdfPagePaths = PdfPageFiles.PathsFor(pagesDirectory, pageCount);

                    RecalculateProgress();
                    ProgressLabel = $"Page {savedPage + 1} of {pageCount} · {Book.ProgressPercent:0}%";
                }
                else
                {
                    PageSource = new HtmlWebViewSource
                    {
                        Html = BookContentService.WrapHtml(
                            "<h2>Could not load PDF pages</h2>" +
                            "<p>This PDF could not be rendered. It may be protected, corrupted, or use an unsupported format.</p>" +
                            "<p>Try re-importing the file.</p>")
                    };
                    Book.TotalChapters = 1;
                    CanGoPrevious = false;
                    CanGoNext = false;
                    Book.ProgressPercent = 0;
                    ProgressLabel = "Render failed";
                }
            }
            else
            {
                IsPdfMode = false;
                PendingScrollPercent = (int)Math.Clamp(Book.ChapterScrollPercent, 0, 100);
                ShowCurrentChapter();
            }

            await SaveProgressAsync(recordHistory: false);
        }
        catch (Exception ex)
        {
            PageSource = new HtmlWebViewSource { Html = BookContentService.WrapHtml($"<p>Error loading book: {ex.Message}</p>") };
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Directory holding the rasterised pages of a book. The directory created at
    /// import time is reused so previously rendered pages are never thrown away.
    /// </summary>
    private static string EnsurePdfPagesDirectory(Book book)
    {
        var directory = book.RenderedPagesDir;

        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Path.Combine(FileSystem.AppDataDirectory, "pages", Guid.NewGuid().ToString("N"));
        }

        Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>
    /// Brings a page directory in line with the current screen: pages rasterised at
    /// another width are dropped, and page files a killed process left half written are
    /// removed. Off the UI thread, because it can delete every page of a long document.
    /// </summary>
    private static async Task PreparePageDirectoryAsync(string pagesDirectory, int renderWidth)
    {
        try
        {
            await Task.Run(() =>
            {
                PdfPageFiles.EnsureRenderWidth(pagesDirectory, renderWidth);
                PdfPageFiles.CleanStaleTempFiles(pagesDirectory);
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"READER: preparing page directory failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Shorter side of the display in physical pixels, or 0 when the platform will not
    /// say. The shorter side is used on purpose: the display width swaps on rotation,
    /// and keying the page resolution off it would make opening the reader in landscape
    /// discard and re-rasterise every page at a new width.
    /// </summary>
    private static int GetShorterDisplaySidePixels()
    {
        try
        {
            var display = DeviceDisplay.MainDisplayInfo;
            return (int)Math.Round(Math.Min(display.Width, display.Height));
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Number of pages to present for a PDF: read from the document itself when it
    /// can be opened, otherwise whatever has already been rasterised to disk.
    /// </summary>
    private async Task<int> ResolvePdfPageCountAsync(string filePath, string pagesDirectory)
    {
        if (_pdfRenderer is not null)
        {
            try
            {
                var count = await _pdfRenderer.GetPageCountAsync(filePath);
                if (count > 0)
                {
                    return count;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"READER: PDF page count failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        return PdfPageFiles.CountRendered(pagesDirectory);
    }



    [RelayCommand]
    public async Task NextAsync()
    {
        if (Book is null || Book.CurrentChapterIndex >= _chapters.Count - 1)
            return;

        Book.CurrentChapterIndex++;
        Book.ChapterScrollPercent = 0;
        PendingScrollPercent = 0;
        ShowCurrentChapter();
        await SaveProgressAsync(recordHistory: false);
    }

    [RelayCommand]
    public async Task PreviousAsync()
    {
        if (Book is null || Book.CurrentChapterIndex <= 0)
            return;

        Book.CurrentChapterIndex--;
        Book.ChapterScrollPercent = 0;
        PendingScrollPercent = 0;
        ShowCurrentChapter();
        await SaveProgressAsync(recordHistory: false);
    }

    public async Task PersistPositionAsync(double scrollPercent, bool recordHistory)
    {
        if (Book is null) return;
        Book.ChapterScrollPercent = Math.Clamp(scrollPercent, 0, 100);
        await SaveProgressAsync(recordHistory);
    }

    public async Task MarkFinishedIfNeededAsync(double scrollPercent)
    {
        if (Book is null || IsPdfMode) return;

        var lastChapter = Book.CurrentChapterIndex >= _chapters.Count - 1;
        if (lastChapter && scrollPercent >= 92)
        {
            Book.ReadingStatus = ReadingStatus.Completed;
            Book.ProgressPercent = 100;
            await _database.SaveBookAsync(Book);
        }
    }

    private void ShowCurrentChapter()
    {
        if (Book is null || _chapters.Count == 0) return;

        var index = Math.Clamp(Book.CurrentChapterIndex, 0, _chapters.Count - 1);
        var chapterContent = _chapters[index];

        PageSource = new HtmlWebViewSource { Html = BookContentService.WrapHtml(chapterContent) };

        Header = Book.Title;
        ProgressLabel = $"{Book.ChapterLabel} · {Book.ProgressPercent:0}%";
        CanGoPrevious = index > 0;
        CanGoNext = index < _chapters.Count - 1;
        RecalculateProgress();
        ProgressLabel = $"{Book.ChapterLabel} · {Book.ProgressPercent:0}%";
    }

    public void UpdatePdfPage(int pageIndex)
    {
        if (Book is null || !IsPdfMode || PdfPagePaths.Count == 0) return;

        var clamped = Math.Clamp(pageIndex, 0, PdfPagePaths.Count - 1);
        Book.CurrentChapterIndex = clamped;
        CurrentPdfPage = clamped;
        RecalculateProgress();
        ProgressLabel = $"Page {clamped + 1} of {PdfPagePaths.Count} · {Book.ProgressPercent:0}%";
    }

    public async Task PersistPdfPositionAsync(bool recordHistory)
    {
        if (Book is null || !IsPdfMode) return;
        await SaveProgressAsync(recordHistory);
        if (PdfPagePaths.Count > 0 && Book.CurrentChapterIndex >= PdfPagePaths.Count - 1)
        {
            Book.ReadingStatus = ReadingStatus.Completed;
            Book.ProgressPercent = 100;
            await _database.SaveBookAsync(Book);
        }
    }

    private void RecalculateProgress()
    {
        if (Book is null) return;

        if (IsPdfMode && PdfPagePaths.Count > 0)
        {
            var pageIndex = Math.Clamp(Book.CurrentChapterIndex, 0, PdfPagePaths.Count - 1);
            Book.ProgressPercent = Math.Clamp(
                ((pageIndex + 1.0) / PdfPagePaths.Count) * 100.0,
                0, 100);
            return;
        }

        if (_chapters.Count == 0) return;

        var chapterShare = 100.0 / _chapters.Count;
        Book.ProgressPercent = Math.Clamp(
            (Book.CurrentChapterIndex * chapterShare) + (Book.ChapterScrollPercent / 100.0 * chapterShare),
            0, 100);
    }

    private async Task SaveProgressAsync(bool recordHistory)
    {
        if (Book is null) return;

        RecalculateProgress();
        Book.LastReadAt = DateTime.Now;
        await _database.SaveBookAsync(Book);
        if (recordHistory)
        {
            await _database.AddHistoryAsync(Book);
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }

    ~ReaderViewModel()
    {
        Dispose(false);
    }
}

