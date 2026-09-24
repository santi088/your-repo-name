using PAGELY.ViewModels;

namespace PAGELY.Views;

[QueryProperty(nameof(BookId), nameof(BookId))]
public partial class BookDetailPage : ContentPage
{
    private readonly BookDetailViewModel _viewModel;

    public BookDetailPage(BookDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public string BookId { get; set; } = string.Empty;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (int.TryParse(BookId, out var id))
        {
            await _viewModel.LoadAsync(id);
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
