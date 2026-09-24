using Microsoft.Extensions.DependencyInjection;
using PAGELY.ViewModels;

namespace PAGELY.Views;

public partial class HistoryPage : ContentPage
{
    private readonly HistoryViewModel _viewModel;

    public HistoryPage() : this(MauiProgram.Services.GetRequiredService<HistoryViewModel>())
    {
    }

    public HistoryPage(HistoryViewModel viewModel)
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
