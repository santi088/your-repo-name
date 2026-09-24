using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PAGELY.Data;
using PAGELY.Models;

namespace PAGELY.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly DatabaseService _database;

    public HistoryViewModel(DatabaseService database)
    {
        _database = database;
    }

    public ObservableCollection<Book> ContinueReading { get; } = [];
    public ObservableCollection<ReadingEvent> Events { get; } = [];

    [ObservableProperty]
    private bool isEmpty = true;

    [RelayCommand]
    public async Task LoadAsync()
    {
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
