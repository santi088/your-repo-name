using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PAGELY.Data;
using PAGELY.Models;

namespace PAGELY.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly DatabaseService _database;

    // The History tab keeps its loaded data: re-querying the database on every
    // tab switch is what made navigation feel slow. The cache is invalidated by
    // DatabaseService.HistoryChanged (a new reading entry or a removed book), so
    // the next appearance reloads exactly when something actually changed.
    private bool _loaded;
    private bool _dirty = true;

    public HistoryViewModel(DatabaseService database)
    {
        _database = database;
        _database.HistoryChanged += OnHistoryChanged;
    }

    private void OnHistoryChanged(object? sender, EventArgs e) => _dirty = true;

    public ObservableCollection<Book> ContinueReading { get; } = [];
    public ObservableCollection<ReadingEvent> Events { get; } = [];

    // Starts false so the "nothing here yet" state waits for the history to load.
    [ObservableProperty]
    private bool isEmpty;

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (_loaded && !_dirty)
        {
            // Already shown and nothing has changed since: keep the in-memory
            // state instead of rebuilding the lists on every navigation.
            return;
        }

        try
        {
            var books = await _database.GetBooksAsync();
            ContinueReading.Clear();
            foreach (var book in books.Where(b => b.LastReadAt is not null)
                         .OrderByDescending(b => b.LastReadAt)
                         .Take(8))
            {
                ContinueReading.Add(book);
            }

            Events.Clear();
            foreach (var item in await _database.GetHistoryAsync())
            {
                Events.Add(item);
            }

            IsEmpty = ContinueReading.Count == 0 && Events.Count == 0;
            _loaded = true;
            _dirty = false;
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Could not load history: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    public async Task OpenBookAsync(Book? book)
    {
        if (book is null)
        {
            return;
        }

        await Shell.Current.GoToAsync($"reader?BookId={book.Id}");
    }

    [RelayCommand]
    public async Task OpenEventAsync(ReadingEvent? item)
    {
        if (item is null)
        {
            return;
        }

        await Shell.Current.GoToAsync($"reader?BookId={item.BookId}");
    }
}
