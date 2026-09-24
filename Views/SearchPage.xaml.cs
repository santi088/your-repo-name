using Microsoft.Extensions.DependencyInjection;
using PAGELY.ViewModels;

namespace PAGELY.Views;

public partial class SearchPage : ContentPage
{
    private readonly SearchViewModel _viewModel;

    public SearchPage() : this(MauiProgram.Services.GetRequiredService<SearchViewModel>())
    {
    }

    public SearchPage(SearchViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private void OnFilterToggleClicked(object? sender, EventArgs e)
    {
        _viewModel.IsFilterVisible = !_viewModel.IsFilterVisible;
    }
}
