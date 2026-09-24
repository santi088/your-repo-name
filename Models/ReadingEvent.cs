using SQLite;

namespace PAGELY.Models;

[Table("ReadingEvents")]
public class ReadingEvent
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int BookId { get; set; }

    public string BookTitle { get; set; } = string.Empty;

    public string BookAuthor { get; set; } = string.Empty;

    public int ChapterIndex { get; set; }

    public double ProgressPercent { get; set; }

    public DateTime ReadAt { get; set; } = DateTime.Now;

    [Ignore]
    public string Summary =>
        $"{BookTitle} · {ProgressPercent:0}% · {ReadAt:MMM d, h:mm tt}";
}
