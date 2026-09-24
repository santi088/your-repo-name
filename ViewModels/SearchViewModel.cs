using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PAGELY.Data;
using PAGELY.Models;

namespace PAGELY.ViewModels;

public partial class SearchViewModel : ObservableObject
{
    private readonly DatabaseService _database;
    private List<Book> _all = [];

    public SearchViewModel(DatabaseService database)
    {
        _database = database;
        StatusFilters = new ObservableCollection<string>(ReadingStatusDisplay.FilterOptions);
        SortOptions = new ObservableCollection<string>(ReadingStatusDisplay.SortOptions);
        Genres = new ObservableCollection<string> { "All genres" };
        SelectedStatus = "All";
        SelectedGenre = "All genres";
        SelectedSort = "Title";
    }

    public ObservableCollection<Book> Books { get; } = [];
    public ObservableCollection<string> StatusFilters { get; }
    public ObservableCollection<string> SortOptions { get; }
    public ObservableCollection<string> Genres { get; }

    [ObservableProperty]
    private string query = string.Empty;

    [ObservableProperty]
    private string selectedStatus = "All";

    [ObservableProperty]
    private string selectedGenre = "All genres";

    [ObservableProperty]
    private string selectedSort = "Title";

    [ObservableProperty]
    private bool isEmpty = true;

    [ObservableProperty]
    private bool isFilterVisible = false;

    partial void OnQueryChanged(string value) => Apply();
    partial void OnSelectedStatusChanged(string value) => Apply();
    partial void OnSelectedGenreChanged(string value) => Apply();
    partial void OnSelectedSortChanged(string value) => Apply();

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            _all = await _database.GetBooksAsync();
            var genres = await _database.GetGenresAsync();
            Genres.Clear();
            Genres.Add("All genres");
            foreach (var genre in genres)
            {
                Genres.Add(genre);
            }

            if (!Genres.Contains(SelectedGenre))
            {
                SelectedGenre = "All genres";
            }

            Apply();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Could not load books: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    public async Task OpenAsync(Book? book)
    {
        if (book is null)
        {
            return;
        }

        await Shell.Current.GoToAsync($"bookdetail?BookId={book.Id}");
    }

    private void Apply()
    {
        IEnumerable<Book> result = _all;

        if (!string.IsNullOrWhiteSpace(Query))
        {
            var q = Query.Trim();
            result = result.Where(b =>
                b.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                b.Author.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                b.Genre.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                b.StatusLabel.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(SelectedStatus, "All", StringComparison.OrdinalIgnoreCase))
        {
            var status = ReadingStatusDisplay.FromLabel(SelectedStatus);
            result = result.Where(b => b.ReadingStatus == status);
        }

        if (!string.Equals(SelectedGenre, "All genres", StringComparison.OrdinalIgnoreCase))
        {
            result = result.Where(b => b.Genre.Equals(SelectedGenre, StringComparison.OrdinalIgnoreCase));
        }

        result = SelectedSort switch
        {
            "Author" => result.OrderBy(b => b.Author).ThenBy(b => b.Title),
            "Genre" => result.OrderBy(b => b.Genre).ThenBy(b => b.Title),
            "Status" => result.OrderBy(b => b.Status).ThenBy(b => b.Title),
            "Recently added" => result.OrderByDescending(b => b.DateAdded),
            "Recently read" => result.OrderByDescending(b => b.LastReadAt ?? DateTime.MinValue),
            "Progress" => result.OrderByDescending(b => b.ProgressPercent).ThenBy(b => b.Title),
            _ => result.OrderBy(b => b.Title)
        };

        Books.Clear();
        foreach (var book in result)
        {
            Books.Add(book);
        }

        IsEmpty = Books.Count == 0;
    }
}
