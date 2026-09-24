using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PAGELY.Data;
using PAGELY.Models;
using PAGELY.Services;

namespace PAGELY.ViewModels;

public partial class LibraryViewModel : ObservableObject
{
    private readonly DatabaseService _database;
    private readonly BookImportService _import;
    private List<Book> _all = [];

    public LibraryViewModel(DatabaseService database, BookImportService import)
    {
        _database = database;
        _import = import;
    }

    public ObservableCollection<Book> Books { get; } = [];

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isEmpty = true;

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            _all = await _database.GetBooksAsync();
            Apply();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Could not load library: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ImportAsync()
    {
        try
        {
            IsBusy = true;
            var book = await _import.ImportAsync();
            if (book is not null)
            {
                await LoadAsync();
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Could not import book", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
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

    [RelayCommand]
    public async Task ContinueAsync(Book? book)
    {
        if (book is null)
        {
            return;
        }

        await Shell.Current.GoToAsync($"reader?BookId={book.Id}");
    }

    private void Apply()
    {
        var list = _all.OrderByDescending(b => b.LastReadAt ?? DateTime.MinValue)
            .ThenBy(b => b.Title)
            .ToList();

        Books.Clear();
        foreach (var book in list)
        {
            Books.Add(book);
        }

        IsEmpty = Books.Count == 0;
    }
}
