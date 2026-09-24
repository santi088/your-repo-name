using Microsoft.Extensions.Logging;
using PAGELY.Data;
using PAGELY.Services;
using PAGELY.ViewModels;
using PAGELY.Views;

namespace PAGELY;

public static class MauiProgram
{
    public static IServiceProvider Services { get; private set; } = default!;

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        SQLitePCL.Batteries_V2.Init();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
            .ConfigureMauiHandlers(handlers =>
            {
#if ANDROID
                handlers.AddHandler<PAGELY.Controls.PdfViewer, PAGELY.Platforms.Android.PdfViewerHandler>();
#endif
            });

        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<BookContentService>();
        builder.Services.AddSingleton<BookImportService>();
        builder.Services.AddSingleton<ReadingStatsService>();

#if ANDROID
        builder.Services.AddSingleton<IPdfRendererService, Platforms.Android.PdfRendererService>();
#else
        builder.Services.AddSingleton<IPdfRendererService, StubPdfRendererService>();
#endif

        builder.Services.AddTransient<LibraryViewModel>();
        builder.Services.AddTransient<SearchViewModel>();
        builder.Services.AddTransient<HistoryViewModel>();
        builder.Services.AddTransient<BookDetailViewModel>();
        builder.Services.AddTransient<ReaderViewModel>(sp =>
            new ReaderViewModel(
                sp.GetRequiredService<DatabaseService>(),
                sp.GetRequiredService<BookContentService>(),
                sp.GetRequiredService<IPdfRendererService>()));
        builder.Services.AddTransient<StatsViewModel>();

        builder.Services.AddTransient<LibraryPage>();
        builder.Services.AddTransient<SearchPage>();
        builder.Services.AddTransient<HistoryPage>();
        builder.Services.AddTransient<BookDetailPage>();
        builder.Services.AddTransient<ReaderPage>();
        builder.Services.AddTransient<StatsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        Services = app.Services;
        return app;
    }
}

internal sealed class StubPdfRendererService : IPdfRendererService
{
    public Task<int> GetPageCountAsync(string filePath, CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task<bool> RenderPageToFileAsync(
        string filePath,
        int pageIndex,
        string outputPath,
        int maxWidth,
        CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
