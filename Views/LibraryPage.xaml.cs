using Microsoft.Extensions.DependencyInjection;
using PAGELY.ViewModels;

namespace PAGELY.Views;

public partial class LibraryPage : ContentPage
{
    private readonly LibraryViewModel _viewModel;

    public LibraryPage() : this(MauiProgram.Services.GetRequiredService<LibraryViewModel>())
    {
    }

    public LibraryPage(LibraryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
