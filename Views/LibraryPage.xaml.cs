using Microsoft.Extensions.DependencyInjection;
using PAGELY.ViewModels;
using System;

namespace PAGELY.Views;

public partial class LibraryPage : ContentPage
{
    private readonly LibraryViewModel _viewModel;

    // Use a clean parameterless constructor wrapper to map compiled source generation graphs
    public LibraryPage()
    {
        InitializeComponent();
        
        _viewModel = MauiProgram.Services.GetRequiredService<LibraryViewModel>();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel != null && _viewModel.LoadCommand != null)
        {
            await _viewModel.LoadCommand.ExecuteAsync(null);
        }
    }

    private void OnStatusClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && _viewModel != null)
        {
            _viewModel.SelectedStatus = button.Text;
        }
    }
}

