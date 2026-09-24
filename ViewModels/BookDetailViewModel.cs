using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PAGELY.Data;
using PAGELY.Models;
using PAGELY.Services;

namespace PAGELY.ViewModels;

public partial class BookDetailViewModel : ObservableObject
{
    private readonly DatabaseService _database;
    private readonly BookImportService _import;

    public BookDetailViewModel(DatabaseService database, BookImportService import)
    {
        _database = database;
        _import = import;
        StatusOptions = ReadingStatusDisplay.FilterOptions.Skip(1).ToList();
    }

    public List<string> StatusOptions { get; }

    [ObservableProperty]
    private Book? book;

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string author = string.Empty;

    [ObservableProperty]
    private string genre = string.Empty;

    [ObservableProperty]
    private string selectedStatus = "To Be Read";

    [RelayCommand]
    public async Task LoadAsync(int id)
    {
        try
        {
            Book = await _database.GetBookAsync(id);
            if (Book is null)
            {
                await Shell.Current.DisplayAlert("Error", "Book not found.", "OK");
                return;
            }

            Title = Book.Title;
            Author = Book.Author;
            Genre = Book.Genre;
            SelectedStatus = Book.StatusLabel;
            OnPropertyChanged(nameof(Book));
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Could not load book details: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (Book is null)
        {
            return;
        }

        Book.Title = string.IsNullOrWhiteSpace(Title) ? "Untitled" : Title.Trim();
        Book.Author = string.IsNullOrWhiteSpace(Author) ? "Unknown author" : Author.Trim();
        Book.Genre = string.IsNullOrWhiteSpace(Genre) ? "Uncategorized" : Genre.Trim();
        Book.ReadingStatus = ReadingStatusDisplay.FromLabel(SelectedStatus);
        await _database.SaveBookAsync(Book);
        await Shell.Current.DisplayAlert("Saved", "Book details were updated.", "OK");
    }

    [RelayCommand]
    public async Task ReadAsync()
    {
        if (Book is null)
        {
            return;
        }

        await Shell.Current.GoToAsync($"reader?BookId={Book.Id}");
    }

    [RelayCommand]
    public async Task DeleteAsync()
    {
        if (Book is null)
        {
            return;
        }

        var confirm = await Shell.Current.DisplayAlert(
            "Remove book",
            $"Remove “{Book.Title}” from PAGELY? The imported copy stored by the app will be deleted.",
            "Remove",
            "Cancel");
        if (!confirm)
        {
            return;
        }

        _import.DeleteStoredFiles(Book);
        await _database.DeleteBookAsync(Book);
        await Shell.Current.GoToAsync("..");
    }
}
