using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PAGELY.Models;
using PAGELY.Services;

namespace PAGELY.ViewModels;

public partial class StatsViewModel : ObservableObject
{
    private readonly ReadingStatsService _statsService;

    public StatsViewModel(ReadingStatsService statsService)
    {
        _statsService = statsService;
    }

    [ObservableProperty]
    private ReadingStats? stats;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isEmpty = true;

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        Stats = await _statsService.GetStatsAsync();
        IsEmpty = Stats?.TotalBooks == 0;
        IsBusy = false;
    }
}