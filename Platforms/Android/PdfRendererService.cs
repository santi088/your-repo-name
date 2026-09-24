using PAGELY.Services;

namespace PAGELY.Platforms.Android;

/// <summary>
/// Android implementation of <see cref="IPdfRendererService"/> built on
/// <c>android.graphics.pdf.PdfRenderer</c>.
/// </summary>
/// <remarks>
/// Every page is rendered, JPEG encoded and written to disk one at a time, so
/// peak memory is a single page bitmap instead of the whole document. That is
/// the difference between "opens instantly and scrolls" and the previous
/// behaviour, which built one base64 string per page and kept the entire list
/// alive until the heap died (OutOfMemoryError on a 163 MB allocation in the
/// field, traced to WebView.loadDataWithBaseURL).
/// </remarks>
public class PdfRendererService : IPdfRendererService
{
    public Task<int> GetPageCountAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => ReadPageCount(filePath), cancellationToken);
    }

    public Task<bool> RenderPageToFileAsync(
        string filePath,
        int pageIndex,
        string outputPath,
        int maxWidth,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => RenderPage(filePath, pageIndex, outputPath, maxWidth, cancellationToken),
            cancellationToken);
    }

    private static int ReadPageCount(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return 0;
            }

            using var fd = Open(filePath);
            using var renderer = new global::Android.Graphics.Pdf.PdfRenderer(fd);
            return renderer.PageCount;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"PDF RENDERER: page count failed: {ex.GetType().Name}: {ex.Message}");
            return 0;
        }
    }

    private static bool RenderPage(
        string filePath,
        int pageIndex,
        string outputPath,
        int maxWidth,
        CancellationToken cancellationToken)
    {
        if (pageIndex < 0 || string.IsNullOrWhiteSpace(outputPath))
        {
            return false;
        }

        string? tempPath = null;

        try
        {
            if (cancellationToken.IsCancellationRequested || !File.Exists(filePath))
            {
                return false;
            }

            using var fd = Open(filePath);
            using var renderer = new global::Android.Graphics.Pdf.PdfRenderer(fd);

            if (pageIndex >= renderer.PageCount)
            {
                return false;
            }

            var page = renderer.OpenPage(pageIndex);
            if (page is null)
            {
                return false;
            }

            using (page)
            {
                var scale = ComputeScale(page.Width, maxWidth);

                var bitmap = global::Android.Graphics.Bitmap.CreateBitmap(
                    Math.Max(1, (int)Math.Round(page.Width * scale)),
                    Math.Max(1, (int)Math.Round(page.Height * scale)),
                    global::Android.Graphics.Bitmap.Config.Argb8888!);

                if (bitmap is null)
                {
                    return false;
                }

                // The bitmap is the only full size allocation in the pipeline and it
                // is released before the next page is even opened.
                using (bitmap)
                {
                    bitmap.EraseColor(global::Android.Graphics.Color.White);

                    using var matrix = new global::Android.Graphics.Matrix();
                    matrix.SetScale(scale, scale);

                    page.Render(
                        bitmap,
                        null,
                        matrix,
                        global::Android.Graphics.Pdf.PdfRenderMode.ForDisplay);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return false;
                    }

                    var directory = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // Encode to a temp file and move it into place so a scrolling
                    // reader can never pick up a half written JPEG.
                    tempPath = outputPath + PdfPageFiles.TempSuffix;

                    using (var stream = File.Create(tempPath))
                    {
                        if (!bitmap.Compress(
                                global::Android.Graphics.Bitmap.CompressFormat.Jpeg!,
                                PdfPageFiles.JpegQuality,
                                stream))
                        {
                            return false;
                        }
                    }

                    if (File.Exists(outputPath))
                    {
                        File.Delete(outputPath);
                    }

                    File.Move(tempPath, outputPath);
                    tempPath = null;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"PDF RENDERER: page {pageIndex} failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
        finally
        {
            if (tempPath is not null)
            {
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch
                {
                }
            }
        }
    }

    private static float ComputeScale(int pageWidth, int maxWidth)
    {
        if (pageWidth <= 0 || maxWidth <= 0)
        {
            return 1f;
        }

        var scale = (float)maxWidth / pageWidth;
        return scale <= 0f ? 1f : scale;
    }

    private static global::Android.OS.ParcelFileDescriptor Open(string filePath)
        => global::Android.OS.ParcelFileDescriptor.Open(
            new global::Java.IO.File(filePath),
            global::Android.OS.ParcelFileMode.ReadOnly)!;
}
