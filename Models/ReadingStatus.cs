namespace PAGELY.Models;

public enum ReadingStatus
{
    ToBeRead,
    CurrentlyReading,
    Completed,
    Dropped
}

public static class ReadingStatusDisplay
{
    public static readonly string[] FilterOptions =
    {
        "All",
        "To Be Read",
        "Currently Reading",
        "Completed",
        "Dropped"
    };

    public static readonly string[] SortOptions =
    {
        "Title",
        "Author",
        "Genre",
        "Status",
        "Recently added",
        "Recently read",
        "Progress"
    };

    public static string ToLabel(ReadingStatus status) => status switch
    {
        ReadingStatus.ToBeRead => "To Be Read",
        ReadingStatus.CurrentlyReading => "Currently Reading",
        ReadingStatus.Completed => "Completed",
        ReadingStatus.Dropped => "Dropped",
        _ => "To Be Read"
    };

    public static ReadingStatus FromLabel(string? label) => label switch
    {
        "Currently Reading" => ReadingStatus.CurrentlyReading,
        "Completed" => ReadingStatus.Completed,
        "Dropped" => ReadingStatus.Dropped,
        _ => ReadingStatus.ToBeRead
    };
}
