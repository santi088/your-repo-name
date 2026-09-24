using PAGELY.Models;
using VersOne.Epub;

namespace PAGELY.Services;

public record BookParseResult(
    string Title,
    string Author,
    string Genre,
    int ChapterCount,
    byte[]? CoverBytes,
    List<string> Chapters);

public class BookContentService
{
    public async Task<BookParseResult> ParseAsync(string filePath, string fileType, string? originalFileName = null)
    {
        return fileType.ToLowerInvariant() switch
        {
            "epub" => await ParseEpubAsync(filePath),
            "html" or "htm" => await ParsePlainAsync(filePath, wrapHtml: false),
            "pdf" => ParsePdf(filePath, originalFileName),
            _ => await ParsePlainAsync(filePath, wrapHtml: true)
        };
    }

    public async Task<List<string>> LoadChaptersAsync(Book book)
    {
        try
        {
            if (!File.Exists(book.FilePath))
            {
                return ["<p>This file is no longer on the device. Import it again from the library.</p>"];
            }

            if (string.Equals(book.FileType, "pdf", StringComparison.OrdinalIgnoreCase))
            {
                return ["__PDF_VIEWER__"];
            }

            var parsed = await ParseAsync(book.FilePath, book.FileType);
            return parsed.Chapters.Count == 0
                ? ["<p>No readable text was found in this file.</p>"]
                : parsed.Chapters;
        }
        catch (Exception ex)
        {
            return [$"<p>Error loading book content: {ex.Message}</p>"];
        }
    }

    public static string WrapHtml(string body) =>
        $$"""
        <!DOCTYPE html>
        <html>
        <head>
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <style>
            html, body {
              margin: 0;
              padding: 0;
              background: #f8f3e7;
              color: #3a3226;
            }
            body {
              font-family: Georgia, "Times New Roman", serif;
              font-size: 18px;
              line-height: 1.75;
              padding: 8px 4px 48px;
            }
            img, svg, video { max-width: 100%; height: auto; display: block; margin: 0 auto; }
            a { color: #7d6a52; }
            h1, h2, h3 { font-weight: 600; line-height: 1.3; }
          </style>
        </head>
        <body>{{body}}</body>
        </html>
        """;

    private static BookParseResult ParsePdf(string filePath, string? originalFileName = null)
    {
        var info = PdfService.ReadInfo(filePath, originalFileName);

        // The page count is not known here: the document has to be opened with the
        // platform PDF engine for that. BookImportService does exactly that right
        // after parsing and overwrites this placeholder chapter count with the real
        // page count from IPdfRendererService.
        return new BookParseResult(info.Title, info.Author, "Uncategorized", 1, info.CoverBytes, ["__PDF_VIEWER__"]);
    }

    private static async Task<BookParseResult> ParseEpubAsync(string filePath)
    {
        var book = await EpubReader.ReadBookAsync(filePath);
        var title = string.IsNullOrWhiteSpace(book.Title) ? Path.GetFileNameWithoutExtension(filePath) : book.Title;
        var author = book.AuthorList is { Count: > 0 }
            ? string.Join(", ", book.AuthorList)
            : (string.IsNullOrWhiteSpace(book.Author) ? "Unknown author" : book.Author);

        var genre = "Uncategorized";
        var subjects = book.Schema?.Package?.Metadata?.Subjects;
        if (subjects is { Count: > 0 } && !string.IsNullOrWhiteSpace(subjects[0].Subject))
        {
            genre = subjects[0].Subject;
        }

        var chapters = book.ReadingOrder
            .Select(item => item.Content)
            .Where(html => !string.IsNullOrWhiteSpace(html))
            .Select(html => html)
            .ToList();

        if (chapters.Count == 0)
        {
            chapters.Add($"<h1>{System.Net.WebUtility.HtmlEncode(title)}</h1><p>This EPUB did not include readable HTML chapters.</p>");
        }

        return new BookParseResult(title, author, genre, chapters.Count, book.CoverImage, chapters);
    }

    private static async Task<BookParseResult> ParsePlainAsync(string filePath, bool wrapHtml)
    {
        var text = await File.ReadAllTextAsync(filePath);
        var title = Path.GetFileNameWithoutExtension(filePath);
        var chunks = ChunkText(text);
        var chapters = chunks
            .Select(chunk => wrapHtml
                ? $"<pre style=\"white-space:pre-wrap;font-family:Georgia,serif;\">{System.Net.WebUtility.HtmlEncode(chunk)}</pre>"
                : chunk)
            .ToList();

        return new BookParseResult(title, "Unknown author", "Uncategorized", chapters.Count, null, chapters);
    }

    private static List<string> ChunkText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return ["This file is empty."];
        }

        const int size = 4000;
        var chunks = new List<string>();
        var remaining = text.Replace("\r\n", "\n");
        while (remaining.Length > 0)
        {
            if (remaining.Length <= size)
            {
                chunks.Add(remaining.Trim());
                break;
            }

            var cut = remaining.LastIndexOf('\n', size);
            if (cut < size / 2)
            {
                cut = size;
            }

            chunks.Add(remaining[..cut].Trim());
            remaining = remaining[cut..];
        }

        return chunks.Where(c => c.Length > 0).DefaultIfEmpty("This file is empty.").ToList();
    }
}
