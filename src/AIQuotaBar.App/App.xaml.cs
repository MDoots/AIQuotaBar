namespace AIQuotaBar.App;

using System.Windows;
using AIQuotaBar.App.ViewModels;
using AIQuotaBar.App.Views;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var viewModel = new WidgetViewModel();
        var window = new WidgetWindow
        {
            DataContext = viewModel
        };

        viewModel.Start();
        window.Show();
    }
}
