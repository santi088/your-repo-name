using PAGELY.ViewModels;

namespace PAGELY.Views;

[QueryProperty(nameof(BookId), nameof(BookId))]
public partial class ReaderPage : ContentPage
{
    private readonly ReaderViewModel _viewModel;

    public ReaderPage(ReaderViewModel viewModel)
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

            if (_viewModel.IsPdfMode && _viewModel.CurrentPdfPage > 0)
            {
                PdfReaderView.ScrollToPage(_viewModel.CurrentPdfPage);
            }
        }
    }

    private void OnPdfPageScrolled(object? sender, Controls.PdfPageScrolledEventArgs e)
    {
        _viewModel.UpdatePdfPage(e.PageIndex);
    }

    protected override async void OnDisappearing()
    {
        if (!_viewModel.IsPdfMode)
        {
            var percent = await ReadScrollPercentAsync();

            await _viewModel.PersistPositionAsync(
                percent,
                recordHistory: true);

            await _viewModel.MarkFinishedIfNeededAsync(percent);
        }
        else
        {
            await _viewModel.PersistPdfPositionAsync(
                recordHistory: true);
        }

        base.OnDisappearing();
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);

        if (_viewModel is IDisposable d)
        {
            d.Dispose();
        }
    }

    private async void OnWebViewNavigated(
        object? sender,
        WebNavigatedEventArgs e)
    {
        if (_viewModel.IsPdfMode)
            return;

        var restore = _viewModel.PendingScrollPercent;

        if (restore <= 0)
            return;

        await ReaderView.EvaluateJavaScriptAsync(
            $"window.scrollTo(0, document.body.scrollHeight * {restore / 100.0});");
    }

    private async Task<double> ReadScrollPercentAsync()
    {
        try
        {
            var result = await ReaderView.EvaluateJavaScriptAsync(
                "(document.body.scrollHeight <= window.innerHeight) ? 100 : (window.scrollY / (document.body.scrollHeight - window.innerHeight) * 100)");

            return double.TryParse(
                result,
                out var value)
                ? Math.Clamp(value, 0, 100)
                : 0;
        }
        catch
        {
            return 0;
        }
    }

    private async void OnBackClicked(
        object? sender,
        EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
