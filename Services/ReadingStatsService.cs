using PAGELY.Data;
using PAGELY.Models;

namespace PAGELY.Services;

public class ReadingStatsService
{
    private readonly DatabaseService _database;

    public ReadingStatsService(DatabaseService database)
    {
        _database = database;
    }

    public async Task<ReadingStats> GetStatsAsync()
    {
        var books = await _database.GetBooksAsync();
        var history = await _database.GetHistoryAsync();
        var now = DateTime.Now;
        var thisMonth = new DateTime(now.Year, now.Month, 1);
        var thisYear = new DateTime(now.Year, 1, 1);

        var stats = new ReadingStats
        {
            TotalBooks = books.Count,
            BooksReadThisMonth = books.Count(b => b.LastReadAt >= thisMonth),
            BooksReadThisYear = books.Count(b => b.LastReadAt >= thisYear),
            CurrentlyReading = books.Count(b => b.ReadingStatus == ReadingStatus.CurrentlyReading),
            ToBeRead = books.Count(b => b.ReadingStatus == ReadingStatus.ToBeRead),
            Completed = books.Count(b => b.ReadingStatus == ReadingStatus.Completed),
            Dropped = books.Count(b => b.ReadingStatus == ReadingStatus.Dropped),
            AverageProgress = books.Count > 0 ? books.Average(b => b.ProgressPercent) : 0
        };

        // Calculate reading streak
        var readingDays = history
            .Select(e => e.ReadAt.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToList();

        stats.RecentReadingDays = readingDays.Take(30).ToList();
        stats.CurrentStreak = CalculateCurrentStreak(readingDays);
        stats.LongestStreak = CalculateLongestStreak(readingDays);

        // Calculate average reading speed (books per month)
        if (stats.TotalBooks > 0)
        {
            var monthsActive = Math.Max(1, (now - books.Min(b => b.DateAdded)).Days / 30.0);
            stats.AverageReadingSpeed = $"{(stats.Completed / monthsActive):F1} books/month";
        }

        // Calculate total reading time estimate (assuming 200 pages/hour)
        var totalPages = books.Sum(b => (int)(b.ProgressPercent / 100 * 300)); // Estimate 300 pages average
        var hours = totalPages / 200.0;
        stats.TotalReadingTime = hours >= 1 ? $"{hours:F1} hours" : $"{(hours * 60):F0} minutes";

        // Get top genres
        stats.TopGenres = books
            .Where(b => !string.IsNullOrWhiteSpace(b.Genre) && b.Genre != "Uncategorized")
            .GroupBy(b => b.Genre)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => g.Key)
            .ToList();

        return stats;
    }

    private static int CalculateCurrentStreak(List<DateTime> readingDays)
    {
        if (readingDays.Count == 0) return 0;

        var streak = 0;
        var today = DateTime.Today;
        var checkDate = today;

        foreach (var day in readingDays)
        {
            if (day == checkDate)
            {
                streak++;
                checkDate = checkDate.AddDays(-1);
            }
            else if (day < checkDate)
            {
                break;
            }
        }

        return streak;
    }

    private static int CalculateLongestStreak(List<DateTime> readingDays)
    {
        if (readingDays.Count == 0) return 0;

        var longestStreak = 0;
        var currentStreak = 1;

        for (int i = 1; i < readingDays.Count; i++)
        {
            if (readingDays[i - 1].Date.AddDays(-1) == readingDays[i].Date)
            {
                currentStreak++;
            }
            else
            {
                longestStreak = Math.Max(longestStreak, currentStreak);
                currentStreak = 1;
            }
        }

        return Math.Max(longestStreak, currentStreak);
    }
}