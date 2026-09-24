namespace PAGELY.Services;

/// <summary>
/// Metadata that can be read from a PDF without parsing the document.
/// </summary>
/// <param name="CoverBytes">
/// Always <c>null</c> for now: PDF cover extraction is not implemented.
/// </param>
public record PdfInfo(
    string Title,
    string Author,
    byte[]? CoverBytes);

/// <summary>
/// Reads the metadata PAGELY stores for a PDF at import time.
/// </summary>
/// <remarks>
/// There is deliberately no page count here. The only trustworthy source for that
/// is <see cref="IPdfRendererService.GetPageCountAsync"/>, which asks the platform
/// PDF engine; see <c>BookImportService</c> (import) and <c>ReaderViewModel</c>
/// (open), which both use it.
///
/// The previous implementation answered it with a regex over the raw file and a
/// "file size / 50 KB" fallback. Nothing ever consumed the number, but computing
/// it read the whole document into a <c>byte[]</c> and then into an ASCII string -
/// twice the file size in garbage, on the UI blocking path, for every PDF import.
/// </remarks>
public static class PdfService
{
    public static PdfInfo ReadInfo(string filePath, string? originalFileName = null)
    {
        var title = !string.IsNullOrWhiteSpace(originalFileName)
            ? Path.GetFileNameWithoutExtension(originalFileName)
            : Path.GetFileNameWithoutExtension(filePath);

        return new PdfInfo(title, "Unknown author", null);
    }

}
