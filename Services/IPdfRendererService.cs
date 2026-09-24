namespace PAGELY.Services;

/// <summary>
/// Rasterises PDF pages to disk.
/// </summary>
/// <remarks>
/// Implementations must stream: one page is rendered, encoded, written and
/// released before the next one starts. Handing page images back in memory (the
/// old contract built a base64 string per page and kept the whole list alive)
/// is what used to exhaust the Android heap and freeze the reader on any
/// document longer than a few dozen pages, so the contract only ever returns
/// file paths.
/// </remarks>
public interface IPdfRendererService
{
    /// <summary>
    /// Number of pages in <paramref name="filePath"/>, or <c>0</c> when the file
    /// cannot be read or is not a PDF.
    /// </summary>
    Task<int> GetPageCountAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders a single page straight to <paramref name="outputPath"/> at
    /// <paramref name="maxWidth"/> pixels wide. Returns <c>false</c> when the
    /// page could not be rendered.
    /// </summary>
    Task<bool> RenderPageToFileAsync(
        string filePath,
        int pageIndex,
        string outputPath,
        int maxWidth,
        CancellationToken cancellationToken = default);
}
