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
    public string TotalReadingTime { get; set; } = "0 hours";
    public string AverageReadingSpeed { get; set; } = "0 pages/day";
    public List<string> TopGenres { get; set; } = [];
    public List<DateTime> RecentReadingDays { get; set; } = [];
}