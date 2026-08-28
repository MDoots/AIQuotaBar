namespace AIQuotaBar.App.Views;

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using AIQuotaBar.App.Layout;
using AIQuotaBar.App.ViewModels;

public partial class WidgetWindow : Window
{
    private const int WM_NCHITTEST = 0x0084;
    private const int WM_EXITSIZEMOVE = 0x0232;
    private const int HTCLIENT = 1;
    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    // Outer window padding for drop shadow is 10px on each side.
    // Resize grip zone: from outer window edge through the 10px shadow margin + 4px inside the visible border.
    private const double ResizeHitThickness = 14.0;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    public Action<double, double, double>? SizeOrPositionChanged { get; set; }
    public Action? OpenSettingsRequested { get; set; }

    public double WidgetContentWidth => Math.Max(ResponsiveLayoutHelper.MinWidgetWidth, ActualWidth - 20.0);

    private bool _isDraggingWindow;
    private POINT _initialCursorPos;
    private RECT _initialWindowRect;
    private IntPtr _hwnd;

    public WidgetWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(_hwnd);
        source?.AddHook(WndProc);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (sizeInfo.WidthChanged)
        {
            if (DataContext is WidgetViewModel vm)
            {
                var contentWidth = Math.Max(ResponsiveLayoutHelper.MinWidgetWidth, sizeInfo.NewSize.Width - 20.0);
                vm.WidgetWidth = contentWidth;
            }

            if (SizeToContent != SizeToContent.Height)
            {
                SizeToContent = SizeToContent.Height;
            }
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

            // Left resize border (horizontal only)
            if (clientPoint.X >= 0 && clientPoint.X <= ResizeHitThickness)
            {
                handled = true;
                return new IntPtr(HTLEFT);
            }

            // Right resize border (horizontal only)
            if (clientPoint.X >= ActualWidth - ResizeHitThickness && clientPoint.X <= ActualWidth)
            {
                handled = true;
                return new IntPtr(HTRIGHT);
            }

            // Explicitly neutralize all other hit test areas (top, bottom, corners, body)
            // to HTCLIENT so Windows never attempts vertical or diagonal sizing,
            // while preserving full WPF client interactivity (buttons, dragging, etc.).
            handled = true;
            return new IntPtr(HTCLIENT);
        }

        if (msg == WM_EXITSIZEMOVE)
        {
            if (WindowState == WindowState.Normal)
            {
                if (SizeToContent != SizeToContent.Height)
                {
                    SizeToContent = SizeToContent.Height;
                }
                InvalidateMeasure();

                var contentWidth = WidgetContentWidth;
                SizeOrPositionChanged?.Invoke(Left, Top, contentWidth);
            }
        }

        return IntPtr.Zero;
    }

    public static (int newLeft, int newTop) CalculateNewPosition(int initialWindowLeft, int initialWindowTop, int initialCursorX, int initialCursorY, int currentCursorX, int currentCursorY)
    {
        int deltaX = currentCursorX - initialCursorX;
        int deltaY = currentCursorY - initialCursorY;
        return (initialWindowLeft + deltaX, initialWindowTop + deltaY);
    }

    public static bool IsInteractiveElement(DependencyObject? element)
    {
        while (element != null && element is not Window)
        {
            if (element is ButtonBase ||
                element is TextBox ||
                element is ScrollBar ||
                element is Thumb ||
                element is Slider)
            {
                return true;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        return false;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        // Do not initiate window drag if clicking an interactive control
        if (IsInteractiveElement(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if (_hwnd == IntPtr.Zero)
        {
            _hwnd = new WindowInteropHelper(this).Handle;
        }

        if (_hwnd != IntPtr.Zero && GetCursorPos(out _initialCursorPos) && GetWindowRect(_hwnd, out _initialWindowRect))
        {
            _isDraggingWindow = true;
            CaptureMouse();
            e.Handled = true;
        }
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingWindow || _hwnd == IntPtr.Zero)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndDrag();
            return;
        }

        if (GetCursorPos(out var currentCursor))
        {
            var (newLeft, newTop) = CalculateNewPosition(
                _initialWindowRect.Left,
                _initialWindowRect.Top,
                _initialCursorPos.X,
                _initialCursorPos.Y,
                currentCursor.X,
                currentCursor.Y);

            SetWindowPos(_hwnd, IntPtr.Zero, newLeft, newTop, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        }
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingWindow)
        {
            EndDrag();
        }
    }

    private void Window_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_isDraggingWindow)
        {
            EndDrag();
        }
    }

    private void EndDrag()
    {
        if (!_isDraggingWindow)
        {
            return;
        }

        _isDraggingWindow = false;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        if (WindowState == WindowState.Normal)
        {
            if (_hwnd != IntPtr.Zero && GetWindowRect(_hwnd, out var rect))
            {
                var dpi = VisualTreeHelper.GetDpi(this);
                Left = rect.Left / dpi.DpiScaleX;
                Top = rect.Top / dpi.DpiScaleY;
            }

            var contentWidth = WidgetContentWidth;
            SizeOrPositionChanged?.Invoke(Left, Top, contentWidth);
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        OpenSettingsRequested?.Invoke();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}
