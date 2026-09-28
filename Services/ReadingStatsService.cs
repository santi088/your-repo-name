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

        // Books the reader is part-way through, most recently touched first.
        stats.InProgress = books
            .Where(b => b.ReadingStatus == ReadingStatus.CurrentlyReading ||
                        (b.ProgressPercent > 0 &&
                         b.ProgressPercent < 100 &&
                         b.ReadingStatus != ReadingStatus.Dropped))
            .OrderByDescending(b => b.LastReadAt ?? DateTime.MinValue)
            .Take(5)
            .Select(b => new BookProgressEntry
            {
                Title = b.Title,
                ProgressPercent = b.ProgressPercent
            })
            .ToList();

        // Finished books, most recently finished first.
        stats.RecentlyFinished = books
            .Where(b => b.ReadingStatus == ReadingStatus.Completed)
            .OrderByDescending(b => b.LastReadAt ?? b.DateAdded)
            .Take(3)
            .Select(b => new FinishedBook
            {
                Title = b.Title,
                FinishedAt = b.LastReadAt ?? b.DateAdded
            })
            .ToList();

        // The last 30 days, oldest first, flagged from the recorded reading days.
        var activeDays = new HashSet<DateTime>(readingDays);
        for (var offset = 29; offset >= 0; offset--)
        {
            var day = DateTime.Today.AddDays(-offset);
            stats.ActivityDays.Add(new ActivityDay
            {
                Date = day,
                IsActive = activeDays.Contains(day)
            });
        }

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