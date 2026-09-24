using SQLite;

namespace PAGELY.Models;

[Table("Books")]
public class Book
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull]
    public string Title { get; set; } = "Untitled";

    public string Author { get; set; } = "Unknown author";

    public string Genre { get; set; } = "Uncategorized";

    [NotNull]
    public string FilePath { get; set; } = string.Empty;

    public string FileType { get; set; } = "txt";

    public string? CoverPath { get; set; }

    public string? RenderedPagesDir { get; set; }

    public int Status { get; set; } = (int)ReadingStatus.ToBeRead;

    public int CurrentChapterIndex { get; set; }

    public double ChapterScrollPercent { get; set; }

    public int TotalChapters { get; set; } = 1;

    public double ProgressPercent { get; set; }

    public DateTime DateAdded { get; set; } = DateTime.Now;

    public DateTime? LastReadAt { get; set; }

    [Ignore]
    public ReadingStatus ReadingStatus
    {
        get => (ReadingStatus)Status;
        set => Status = (int)value;
    }

    [Ignore]
    public string StatusLabel => ReadingStatusDisplay.ToLabel(ReadingStatus);

    [Ignore]
    public string ProgressLabel => $"{ProgressPercent:0}%";

    [Ignore]
    public string ChapterLabel =>
        TotalChapters <= 0
            ? "No chapters"
            : $"Chapter {Math.Min(CurrentChapterIndex + 1, TotalChapters)} of {TotalChapters}";

    [Ignore]
    public string DateAddedLabel => DateAdded.ToString("MMM d, yyyy");

    [Ignore]
    public string LastReadLabel =>
        LastReadAt is null ? "Not opened yet" : $"Last read {LastReadAt:MMM d, yyyy h:mm tt}";

    [Ignore]
    public string Initial =>
        string.IsNullOrWhiteSpace(Title) ? "P" : Title.Trim()[..1].ToUpperInvariant();
}
