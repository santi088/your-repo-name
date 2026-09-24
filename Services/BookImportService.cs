using PAGELY.Data;
using PAGELY.Models;
using PAGELY.Services;

namespace PAGELY.Services;

public class BookImportService
{
    private readonly DatabaseService _database;
    private readonly BookContentService _content;
    private readonly IPdfRendererService _pdfRenderer;

    public BookImportService(DatabaseService database, BookContentService content, IPdfRendererService pdfRenderer)
    {
        _database = database;
        _content = content;
        _pdfRenderer = pdfRenderer;
    }

    public async Task<Book?> ImportAsync()
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Import a book",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.Android, new[] { "application/epub+zip", "text/plain", "text/html", "text/html", "application/pdf" } },
                    { DevicePlatform.WinUI, new[] { ".epub", ".txt", ".html", ".htm", ".pdf" } },
                    { DevicePlatform.iOS, new[] { "org.idpf.epub-container", "public.plain-text", "public.html", "com.adobe.pdf" } },
                    { DevicePlatform.MacCatalyst, new[] { "epub", "txt", "html", "htm", "pdf" } }
                })
            });

            if (result is null)
            {
                return null;
            }

            var extension = Path.GetExtension(result.FileName).TrimStart('.').ToLowerInvariant();
            if (extension is not ("epub" or "txt" or "html" or "htm" or "pdf"))
            {
                throw new InvalidOperationException("PAGELY imports EPUB, TXT, HTML, and PDF books.");
            }

            var booksDir = Path.Combine(FileSystem.AppDataDirectory, "books");
            var coversDir = Path.Combine(FileSystem.AppDataDirectory, "covers");
            var pagesDir = Path.Combine(FileSystem.AppDataDirectory, "pages");
            Directory.CreateDirectory(booksDir);
            Directory.CreateDirectory(coversDir);
            Directory.CreateDirectory(pagesDir);

            var storedName = $"{Guid.NewGuid():N}.{extension}";
            var storedPath = Path.Combine(booksDir, storedName);

            await using (var source = await result.OpenReadAsync())
            await using (var target = File.Create(storedPath))
            {
                await source.CopyToAsync(target);
            }

            var parsed = await _content.ParseAsync(storedPath, extension, result.FileName);
            string? coverPath = null;
            if (parsed.CoverBytes is { Length: > 0 })
            {
                coverPath = Path.Combine(coversDir, $"{Path.GetFileNameWithoutExtension(storedName)}.img");
                await File.WriteAllBytesAsync(coverPath, parsed.CoverBytes);
            }

            string? renderedPagesDir = null;
            var totalChapters = Math.Max(1, parsed.ChapterCount);

            if (extension == "pdf")
            {
                var bookPagesDir = Path.Combine(pagesDir, Path.GetFileNameWithoutExtension(storedName));
                Directory.CreateDirectory(bookPagesDir);
                renderedPagesDir = bookPagesDir;

                // Pages are rasterised the first time they scroll into view, so
                // importing a long document stays fast and never loads it into memory.
                try
                {
                    var pageCount = await _pdfRenderer.GetPageCountAsync(storedPath);
                    if (pageCount > 0)
                    {
                        totalChapters = pageCount;
                    }
                }
                catch (Exception ex)
                {
                    // The book stays usable: both BookDetailViewModel and the reader
                    // retry this count, so failing here only costs accuracy until one
                    // of them fills it in.
                    Console.WriteLine(
                        $"IMPORT: PDF page count failed: {ex.GetType().Name}: {ex.Message}");
                }
            }

            var book = new Book
            {
                Title = parsed.Title,
                Author = parsed.Author,
                Genre = parsed.Genre,
                FilePath = storedPath,
                FileType = extension,
                CoverPath = coverPath,
                RenderedPagesDir = renderedPagesDir,
                TotalChapters = totalChapters,
                ReadingStatus = ReadingStatus.ToBeRead,
                DateAdded = DateTime.Now
            };

            await _database.SaveBookAsync(book);
            return book;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to import book: {ex.Message}", ex);
        }
    }

    public void DeleteStoredFiles(Book book)
    {
        TryDelete(book.FilePath);
        TryDelete(book.CoverPath);
        TryDeleteDir(book.RenderedPagesDir);
    }

    private static void TryDelete(string? path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static void TryDeleteDir(string? path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
        }
    }
}
