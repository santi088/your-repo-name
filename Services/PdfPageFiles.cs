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
}
