using PAGELY.Views;

namespace PAGELY;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("bookdetail", typeof(BookDetailPage));
        Routing.RegisterRoute("reader", typeof(ReaderPage));
    }
}
