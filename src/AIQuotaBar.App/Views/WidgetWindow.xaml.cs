namespace AIQuotaBar.App.Views;

using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using AIQuotaBar.App.Layout;
using AIQuotaBar.App.ViewModels;

public partial class WidgetWindow : Window
{
    private const int WM_NCHITTEST = 0x0084;
    private const int WM_EXITSIZEMOVE = 0x0232;
    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;

    // Outer window padding for drop shadow is 10px on each side.
    // Resize grip zone: from outer window edge through the 10px shadow margin + 4px inside the visible border.
    private const double ResizeHitThickness = 14.0;

    public Action<double, double>? PositionChanged { get; set; }
    public Action<double, double, double>? SizeOrPositionChanged { get; set; }

    public double WidgetContentWidth => Math.Max(ResponsiveLayoutHelper.MinWidgetWidth, ActualWidth - 20.0);

    public WidgetWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(hwnd);
        source?.AddHook(WndProc);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (sizeInfo.WidthChanged && DataContext is WidgetViewModel vm)
        {
            var contentWidth = Math.Max(ResponsiveLayoutHelper.MinWidgetWidth, sizeInfo.NewSize.Width - 20.0);
            vm.WidgetWidth = contentWidth;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_NCHITTEST)
        {
            if (!IsLoaded || ActualWidth <= 0 || ActualHeight <= 0)
            {
                return IntPtr.Zero;
            }

            int screenX = unchecked((short)(long)lParam);
            int screenY = unchecked((short)((long)lParam >> 16));

            Point clientPoint;
            try
            {
                clientPoint = PointFromScreen(new Point(screenX, screenY));
            }
            catch
            {
                return IntPtr.Zero;
            }

            // Left resize border
            if (clientPoint.X >= 0 && clientPoint.X <= ResizeHitThickness)
            {
                handled = true;
                return new IntPtr(HTLEFT);
            }

            // Right resize border
            if (clientPoint.X >= ActualWidth - ResizeHitThickness && clientPoint.X <= ActualWidth)
            {
                handled = true;
                return new IntPtr(HTRIGHT);
            }

            return IntPtr.Zero;
        }

        if (msg == WM_EXITSIZEMOVE)
        {
            if (WindowState == WindowState.Normal)
            {
                var contentWidth = WidgetContentWidth;
                SizeOrPositionChanged?.Invoke(Left, Top, contentWidth);
                PositionChanged?.Invoke(Left, Top);
            }
        }

        return IntPtr.Zero;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();

            // DragMove blocks until mouse button is released.
            // Persist the settled position and size once dragging completes.
            if (WindowState == WindowState.Normal)
            {
                var contentWidth = WidgetContentWidth;
                SizeOrPositionChanged?.Invoke(Left, Top, contentWidth);
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
