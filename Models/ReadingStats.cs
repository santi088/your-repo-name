namespace PAGELY.Models;

public class ReadingStats
{
    public int TotalBooks { get; set; }
    public int BooksReadThisMonth { get; set; }
    public int BooksReadThisYear { get; set; }
    public int CurrentlyReading { get; set; }
    public int ToBeRead { get; set; }
    public int Completed { get; set; }
    public int Dropped { get; set; }
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public double AverageProgress { get; set; }
    public List<DateTime> RecentReadingDays { get; set; } = [];

    /// <summary>Books still being read, most recently touched first.</summary>
    public List<BookProgressEntry> InProgress { get; set; } = [];

    /// <summary>Completed books, most recently finished first.</summary>
    public List<FinishedBook> RecentlyFinished { get; set; } = [];

    /// <summary>The last 30 days, oldest first, flagged by whether anything was read.</summary>
    public List<ActivityDay> ActivityDays { get; set; } = [];

    public bool HasInProgress => InProgress.Count > 0;
    public bool HasRecentlyFinished => RecentlyFinished.Count > 0;
    public bool HasActivity => ActivityDays.Exists(d => d.IsActive);
}

/// <summary>Progress of one book the reader is part-way through.</summary>
public class BookProgressEntry
{
    public string Title { get; set; } = string.Empty;
    public double ProgressPercent { get; set; }
}

/// <summary>A finished book and when it was last read.</summary>
public class FinishedBook
{
    public string Title { get; set; } = string.Empty;
    public DateTime FinishedAt { get; set; }
}

/// <summary>One day of the recent-activity strip.</summary>
public class ActivityDay
{
    public DateTime Date { get; set; }
    public bool IsActive { get; set; }
}