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
        LocationChanged += OnLocationChanged;
    }

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Normal && Left >= -5000 && Top >= -5000)
        {
            PositionChanged?.Invoke(Left, Top);
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
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
