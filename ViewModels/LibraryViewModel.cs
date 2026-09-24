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
        StatusFilters = new ObservableCollection<string>(ReadingStatusDisplay.FilterOptions);
        SelectedStatus = "All";
    }

    public ObservableCollection<Book> Books { get; } = [];
    public ObservableCollection<string> StatusFilters { get; }

    [ObservableProperty]
    private string selectedStatus = "All";

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isEmpty = true;

    [ObservableProperty]
    private string emptyTitle = "Your shelves are empty";

    [ObservableProperty]
    private string emptyMessage = "Import EPUB or TXT books already on this device. PAGELY works fully offline.";

    partial void OnSelectedStatusChanged(string value) => ApplyFilter();

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            _all = await _database.GetBooksAsync();
            ApplyFilter();
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

    private void ApplyFilter()
    {
        IEnumerable<Book> query = _all;
        if (!string.Equals(SelectedStatus, "All", StringComparison.OrdinalIgnoreCase))
        {
            var status = ReadingStatusDisplay.FromLabel(SelectedStatus);
            query = query.Where(b => b.ReadingStatus == status);
        }

        var list = query.OrderByDescending(b => b.LastReadAt ?? DateTime.MinValue)
            .ThenBy(b => b.Title)
            .ToList();

        Books.Clear();
        foreach (var book in list)
        {
            Books.Add(book);
        }

        IsEmpty = Books.Count == 0;
        if (_all.Count == 0)
        {
            EmptyTitle = "Your shelves are empty";
            EmptyMessage = "Import EPUB or TXT books already on this device. PAGELY works fully offline.";
        }
        else
        {
            EmptyTitle = "Nothing in this shelf";
            EmptyMessage = "Try another reading status, or import another book.";
        }
    }
}
