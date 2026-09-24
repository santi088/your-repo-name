using System.Text.RegularExpressions;

namespace PAGELY.Services;

public record PdfInfo(
    string Title,
    string Author,
    int PageCount,
    byte[]? CoverBytes);

public partial class PdfService : IAsyncDisposable
{
    private readonly string _filePath;
    private bool _disposed;

    public int PageCount { get; private set; }

    public PdfService(string filePath)
    {
        _filePath = filePath;
    }

    public void Open()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PdfService));

        PageCount = EstimatePageCount(_filePath);
    }

    public static PdfInfo ReadInfo(string filePath, string? originalFileName = null)
    {
        var title = !string.IsNullOrWhiteSpace(originalFileName)
            ? Path.GetFileNameWithoutExtension(originalFileName)
            : Path.GetFileNameWithoutExtension(filePath);
        var author = "Unknown author";
        var pageCount = EstimatePageCount(filePath);

        return new PdfInfo(title, author, pageCount, null);
    }

    private static int EstimatePageCount(string filePath)
    {
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var text = System.Text.Encoding.ASCII.GetString(bytes);

            var matches = PdfPageCountRegex().Matches(text);
            if (matches.Count > 0)
            {
                return Math.Max(1, matches.Count);
            }

            var estimated = (int)(bytes.Length / 50_000);
            return Math.Max(1, estimated);
        }
        catch
        {
            return 1;
        }
    }

    [GeneratedRegex(@"/Type\s*/Page[^s]", RegexOptions.Compiled)]
    private static partial Regex PdfPageCountRegex();

    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
        return ValueTask.CompletedTask;
    }
}
