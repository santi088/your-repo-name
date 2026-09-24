namespace PAGELY.Controls;

public class PdfViewer : View
{
    public static readonly BindableProperty PagePathsProperty =
        BindableProperty.Create(
            nameof(PagePaths),
            typeof(IReadOnlyList<string>),
            typeof(PdfViewer),
            null);

    /// <summary>
    /// Path of the source PDF. Pages that have not been rasterised yet are
    /// rendered on demand from this file as they scroll into view.
    /// </summary>
    public static readonly BindableProperty SourcePathProperty =
        BindableProperty.Create(
            nameof(SourcePath),
            typeof(string),
            typeof(PdfViewer),
            default(string));

    public static readonly BindableProperty CurrentPageIndexProperty =
        BindableProperty.Create(
            nameof(CurrentPageIndex),
            typeof(int),
            typeof(PdfViewer),
            0,
            BindingMode.TwoWay);

    /// <summary>
    /// Width in pixels pages should be rasterised at, decided by the view model so the
    /// width stamped onto the page files and the width the native viewer requests can
    /// never disagree. <c>0</c> lets the platform handler choose.
    /// </summary>
    public static readonly BindableProperty RenderWidthProperty =
        BindableProperty.Create(
            nameof(RenderWidth),
            typeof(int),
            typeof(PdfViewer),
            0);

    public IReadOnlyList<string>? PagePaths
    {
        get => (IReadOnlyList<string>?)GetValue(PagePathsProperty);
        set => SetValue(PagePathsProperty, value);
    }

    public string? SourcePath
    {
        get => (string?)GetValue(SourcePathProperty);
        set => SetValue(SourcePathProperty, value);
    }

    public int CurrentPageIndex
    {
        get => (int)GetValue(CurrentPageIndexProperty);
        set => SetValue(CurrentPageIndexProperty, value);
    }

    public int RenderWidth
    {
        get => (int)GetValue(RenderWidthProperty);
        set => SetValue(RenderWidthProperty, value);
    }

    public event EventHandler<PdfPageScrolledEventArgs>? PageScrolled;

    public void NotifyPageScrolled(int pageIndex, int totalPages, double scrollPercent)
    {
        CurrentPageIndex = pageIndex;
        PageScrolled?.Invoke(this, new PdfPageScrolledEventArgs(pageIndex, totalPages, scrollPercent));
    }

    public void ScrollToPage(int pageIndex)
    {
        Handler?.Invoke(nameof(ScrollToPage), pageIndex);
    }
}

public class PdfPageScrolledEventArgs : EventArgs
{
    public int PageIndex { get; }
    public int TotalPages { get; }
    public double ScrollPercent { get; }

    public PdfPageScrolledEventArgs(int pageIndex, int totalPages, double scrollPercent)
    {
        PageIndex = pageIndex;
        TotalPages = totalPages;
        ScrollPercent = scrollPercent;
    }
}
