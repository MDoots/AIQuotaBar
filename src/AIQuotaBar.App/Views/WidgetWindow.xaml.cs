namespace AIQuotaBar.App.Views;

using System.Windows;
using System.Windows.Input;
using AIQuotaBar.App.ViewModels;

public partial class WidgetWindow : Window
{
    public Action<double, double>? PositionChanged { get; set; }

    public WidgetWindow()
    {
        InitializeComponent();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();

            // DragMove blocks until mouse button is released.
            // Persist the settled position once when dragging completes.
            if (WindowState == WindowState.Normal)
            {
                PositionChanged?.Invoke(Left, Top);
            }
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        // Hide to system tray
        Hide();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        // Genuine application shutdown
        Application.Current.Shutdown();
    }
}
