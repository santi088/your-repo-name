using System.Globalization;

namespace PAGELY.Services;

/// <summary>
/// Single source of truth for the on-disk names used for rasterised PDF pages.
/// The renderer, the reader view model and the native page adapter all have to
/// agree on these names, so they are defined here and nowhere else.
/// </summary>
public static class PdfPageFiles
{
    /// <summary>Glob used to find previously rendered pages inside a book's page directory.</summary>
    public const string SearchPattern = "page_*.jpg";

    /// <summary>JPEG quality used when a page is written to disk.</summary>
    public const int JpegQuality = 82;

    /// <summary>
    /// Suffix a page is written under before it is moved into place, so anything
    /// scanning <see cref="SearchPattern"/> can never pick up a half written file.
    /// </summary>
    public const string TempSuffix = ".tmp";

    /// <summary>File recording the width the pages in a directory were rendered at.</summary>
    public const string RenderWidthStampFileName = ".render_width";

    /// <summary>Pages are never rasterised narrower than this, whatever the screen reports.</summary>
    public const int MinRenderWidth = 720;

    /// <summary>
    /// Upper bound on the width of a rasterised page. Rendering wider than this only
    /// costs JPEG bytes on disk and pixels in the decode, never detail on screen.
    /// </summary>
    public const int MaxRenderWidth = 1600;

    /// <summary>
    /// Upper bound on the pixels of a single page, assuming an A4 shaped page. This is
    /// what keeps one page's decoded bitmap inside the viewer's 32 MB cache budget.
    /// </summary>
    private const double MaxRenderPixels = 4_000_000;

    /// <summary>Height divided by width of an A4 page.</summary>
    private const double PortraitPageAspect = 1.414;

    public static string FileName(int pageIndex) => $"page_{pageIndex:D4}.jpg";

    public static string PathFor(string directory, int pageIndex)
        => Path.Combine(directory, FileName(pageIndex));

    /// <summary>
    /// Builds the page path list for a document. Paths are returned whether or not
    /// the file exists yet: pages are rasterised on demand by the viewer.
    /// </summary>
    public static List<string> PathsFor(string directory, int pageCount)
    {
        if (pageCount <= 0)
        {
            return [];
        }

        var paths = new List<string>(pageCount);
        for (var i = 0; i < pageCount; i++)
        {
            paths.Add(PathFor(directory, i));
        }

        return paths;
    }

    /// <summary>Number of pages already rasterised in <paramref name="directory"/>.</summary>
    public static int CountRendered(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return 0;
        }

        try
        {
            return Directory.GetFiles(directory, SearchPattern).Length;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Width a page should be rasterised at on a display whose shorter side is
    /// <paramref name="displaySidePixels"/> physical pixels.
    /// </summary>
    /// <remarks>
    /// Never wider than the display (so the viewer never has to upscale a page) and
    /// never wide enough for one page's bitmap to blow the viewer's cache budget.
    /// Callers pass the shorter side on purpose: the other side swaps on rotation, and a
    /// width that changed with orientation would discard and re-rasterise every page.
    /// A value of 0 means the platform would not report a display size, in which case the
    /// widest allowed page is used.
    /// </remarks>
    public static int ComputeRenderWidth(int displaySidePixels)
    {
        var widthForPixelBudget = (int)Math.Sqrt(MaxRenderPixels / PortraitPageAspect);
        var upperBound = Math.Max(MinRenderWidth, Math.Min(MaxRenderWidth, widthForPixelBudget));

        return displaySidePixels <= 0
            ? upperBound
            : Math.Clamp(displaySidePixels, MinRenderWidth, upperBound);
    }

    /// <summary>
    /// Makes <paramref name="directory"/> hold pages rasterised at
    /// <paramref name="renderWidth"/>, discarding pages left there by another width
    /// (a different screen, a resized window, a density change).
    /// </summary>
    /// <returns><c>true</c> when stale pages were thrown away.</returns>
    /// <remarks>
    /// Keeping pages rendered at another width would show a soft page next to a sharp
    /// one. Dropping them is safe: pages are rasterised lazily, so the cost is paid
    /// again only for the pages that are actually looked at.
    /// </remarks>
    public static bool EnsureRenderWidth(string? directory, int renderWidth)
    {
        if (string.IsNullOrWhiteSpace(directory) || renderWidth <= 0 || !Directory.Exists(directory))
        {
            return false;
        }

        try
        {
            var stampPath = Path.Combine(directory, RenderWidthStampFileName);

            if (File.Exists(stampPath)
                && int.TryParse(
                    File.ReadAllText(stampPath).Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var stampedWidth)
                && stampedWidth == renderWidth)
            {
                return false;
            }

            foreach (var file in Directory.GetFiles(directory, SearchPattern))
            {
                TryDelete(file);
            }

            CleanStaleTempFiles(directory);
            File.WriteAllText(stampPath, renderWidth.ToString(CultureInfo.InvariantCulture));

            Console.WriteLine($"PDF PAGES: page width set to {renderWidth}px");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"PDF PAGES: page width check failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Removes page files left half written by a process that died mid render.
    /// Only call this when nothing is rendering into <paramref name="directory"/>.
    /// </summary>
    public static void CleanStaleTempFiles(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return;
        }

        try
        {
            foreach (var file in Directory.GetFiles(directory, "*" + TempSuffix))
            {
                TryDelete(file);
            }
        }
        catch
        {
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }
}
