using Microsoft.Extensions.DependencyInjection;
using PAGELY.ViewModels;

namespace PAGELY.Views;

public partial class StatsPage : ContentPage
{
    private readonly StatsViewModel _viewModel;

    public StatsPage() : this(MauiProgram.Services.GetRequiredService<StatsViewModel>())
    {
    }

    public StatsPage(StatsViewModel viewModel)
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