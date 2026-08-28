namespace AIQuotaBar.App.Views;

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using AIQuotaBar.App.Layout;
using AIQuotaBar.App.Settings;
using AIQuotaBar.App.ViewModels;

public partial class WidgetWindow : Window
{
    private const int WM_NCHITTEST = 0x0084;
    private const int WM_EXITSIZEMOVE = 0x0232;
    private const int WM_DPICHANGED = 0x02E0;
    private const int WM_DISPLAYCHANGE = 0x007E;
    private const int WM_SETTINGCHANGE = 0x001A;

    private const int HTCLIENT = 1;
    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    private const uint MONITOR_DEFAULTTONEAREST = 2;

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

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
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

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    public Action<double, double, double>? SizeOrPositionChanged { get; set; }
    public Action? OpenSettingsRequested { get; set; }

    public double WidgetContentWidth => Math.Max(ResponsiveLayoutHelper.MinWidgetWidth, ActualWidth - 20.0);

    public bool IsSettingsOpen { get; set; }

    private bool _isDraggingWindow;
    private bool _isContextMenuOpen;
    private WidgetDockMode _initialDockMode = WidgetDockMode.Floating;
    private POINT _initialCursorPos;
    private RECT _initialWindowRect;
    private IntPtr _hwnd;
    private bool _isReanchoring;
    private readonly DispatcherTimer _autoHideTimer;

    public WidgetWindow()
    {
        InitializeComponent();

        _autoHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(800)
        };
        _autoHideTimer.Tick += OnAutoHideTimerTick;

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is WidgetViewModel oldVm)
        {
            oldVm.VisibilityStateUpdated -= OnViewModelVisibilityOrQuotaStateUpdated;
            oldVm.QuotaStateUpdated -= OnViewModelVisibilityOrQuotaStateUpdated;
            oldVm.AutoHideDockedBarChanged -= OnAutoHideDockedBarChanged;
            oldVm.DockedHorizontalAnchorChanged -= OnDockedHorizontalAnchorChanged;
        }
        if (e.NewValue is WidgetViewModel newVm)
        {
            newVm.VisibilityStateUpdated += OnViewModelVisibilityOrQuotaStateUpdated;
            newVm.QuotaStateUpdated += OnViewModelVisibilityOrQuotaStateUpdated;
            newVm.AutoHideDockedBarChanged += OnAutoHideDockedBarChanged;
            newVm.DockedHorizontalAnchorChanged += OnDockedHorizontalAnchorChanged;
        }
    }

    private void OnAutoHideDockedBarChanged(bool isEnabled)
    {
        if (!isEnabled && DataContext is WidgetViewModel vm && vm.IsDockCollapsed)
        {
            _autoHideTimer.Stop();
            vm.IsDockCollapsed = false;
            ReanchorDockedWindow();
        }
    }

    private void OnDockedHorizontalAnchorChanged(double anchor)
    {
        if (DataContext is WidgetViewModel vm && vm.IsDockedMode && !_isDraggingWindow && IsLoaded)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => ReanchorDockedWindow()));
        }
    }

    private void OnViewModelVisibilityOrQuotaStateUpdated()
    {
        if (DataContext is WidgetViewModel vm && vm.IsDockedMode && IsLoaded)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => ReanchorDockedWindow()));
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (_hwnd == IntPtr.Zero)
        {
            _hwnd = new WindowInteropHelper(this).Handle;
        }
        var source = HwndSource.FromHwnd(_hwnd);
        source?.AddHook(WndProc);

        if (DataContext is WidgetViewModel vm && vm.IsDockedMode)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => ReanchorDockedWindow()));
        }
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);

        if (DataContext is WidgetViewModel vm)
        {
            if (vm.IsFloatingMode)
            {
                if (sizeInfo.WidthChanged)
                {
                    var contentWidth = Math.Max(ResponsiveLayoutHelper.MinWidgetWidth, sizeInfo.NewSize.Width - 20.0);
                    vm.WidgetWidth = contentWidth;

                    if (SizeToContent != SizeToContent.Height)
                    {
                        SizeToContent = SizeToContent.Height;
                    }
                }
            }
            else if (vm.IsDockedMode)
            {
                if (sizeInfo.HeightChanged || sizeInfo.WidthChanged)
                {
                    if (!_isReanchoring && IsLoaded)
                    {
                        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => ReanchorDockedWindow()));
                    }
                }
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

            // In Docked mode, resize grips are disabled completely
            if (DataContext is WidgetViewModel vm && !vm.IsFloatingMode)
            {
                handled = true;
                return new IntPtr(HTCLIENT);
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

            // Left resize border (horizontal only, floating mode only)
            if (clientPoint.X >= 0 && clientPoint.X <= ResizeHitThickness)
            {
                handled = true;
                return new IntPtr(HTLEFT);
            }

            // Right resize border (horizontal only, floating mode only)
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
                if (DataContext is WidgetViewModel vm && vm.IsFloatingMode)
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
        }

        if (msg is WM_DPICHANGED or WM_DISPLAYCHANGE or WM_SETTINGCHANGE)
        {
            if (DataContext is WidgetViewModel vm && vm.IsDockedMode)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => ReanchorDockedWindow()));
            }
        }

        return IntPtr.Zero;
    }

    public bool GetNearestMonitorInfo(out RECT workArea, out RECT monitorArea)
    {
        workArea = new RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };
        monitorArea = new RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };

        IntPtr hMonitor = IntPtr.Zero;
        if (_hwnd != IntPtr.Zero)
        {
            hMonitor = MonitorFromWindow(_hwnd, MONITOR_DEFAULTTONEAREST);
        }

        if (hMonitor != IntPtr.Zero)
        {
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(hMonitor, ref mi))
            {
                workArea = mi.rcWork;
                monitorArea = mi.rcMonitor;
                return true;
            }
        }

        // Fallback to WinForms Screen primary or first
        try
        {
            var primary = System.Windows.Forms.Screen.PrimaryScreen?.WorkingArea
                          ?? (System.Windows.Forms.Screen.AllScreens.Length > 0
                              ? System.Windows.Forms.Screen.AllScreens[0].WorkingArea
                              : new System.Drawing.Rectangle(0, 0, 1920, 1080));

            workArea = new RECT
            {
                Left = primary.Left,
                Top = primary.Top,
                Right = primary.Right,
                Bottom = primary.Bottom
            };
            monitorArea = workArea;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void ApplyDockMode(
        WidgetDockMode dockMode,
        double? restoreFloatingLeft = null,
        double? restoreFloatingTop = null,
        double? restoreFloatingWidth = null)
    {
        if (dockMode == WidgetDockMode.Floating)
        {
            // Restore Floating mode rules
            MinWidth = 170.0;
            MaxWidth = 580.0;

            var targetContentWidth = ResponsiveLayoutHelper.ClampWidth(
                restoreFloatingWidth ?? (DataContext as WidgetViewModel)?.WidgetWidth);
            var outerWidth = targetContentWidth + 20.0;
            Width = outerWidth;

            if (DataContext is WidgetViewModel vm)
            {
                vm.WidgetWidth = targetContentWidth;
            }

            var safePos = PositionHelper.GetSafePosition(
                restoreFloatingLeft,
                restoreFloatingTop,
                windowWidth: outerWidth);

            Left = safePos.Left;
            Top = safePos.Top;

            if (_hwnd != IntPtr.Zero)
            {
                var dpi = VisualTreeHelper.GetDpi(this);
                int pxLeft = (int)Math.Round(safePos.Left * dpi.DpiScaleX);
                int pxTop = (int)Math.Round(safePos.Top * dpi.DpiScaleY);
                SetWindowPos(_hwnd, IntPtr.Zero, pxLeft, pxTop, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
            }
        }
        else
        {
            // Docked Mode: expand MaxWidth limit and position
            MinWidth = 170.0;
            MaxWidth = double.PositiveInfinity;

            ReanchorDockedWindow(dockMode);
        }
    }

    public double MeasureDesiredDockedOuterWidth()
    {
        // 1. Measure drag grip
        DockedDragGrip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var gripWidth = DockedDragGrip.DesiredSize.Width;

        // 2. Measure center content (either providers items control or empty state)
        double centerWidth = 0;
        if (DataContext is WidgetViewModel vm && vm.ShowEmptyState)
        {
            DockedEmptyStateBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            centerWidth = DockedEmptyStateBorder.DesiredSize.Width;
        }
        else
        {
            DockedItemsControl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            centerWidth = DockedItemsControl.DesiredSize.Width;
        }

        // 3. Measure overflow menu button
        DockedMenuButton.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var menuBtnWidth = DockedMenuButton.DesiredSize.Width;

        // 4. Sum up internal margins and paddings:
        // DockedContentGrid margin = 4 on left, 4 on right (8)
        // DockedDragGrip margin = 2 left, 2 right (4)
        // Center content margin = 4 right (4)
        // Menu button container margin = 2 right (2)
        // Extra padding buffer = 2 (for border thickness and sub-pixel rounding)
        var totalSpacing = 8.0 + 4.0 + 4.0 + 2.0 + 2.0;

        var desiredOuterWidth = gripWidth + centerWidth + menuBtnWidth + totalSpacing;
        return desiredOuterWidth;
    }

    public void ReanchorDockedWindow(WidgetDockMode? modeOverride = null)
    {
        if (_isReanchoring)
        {
            return;
        }

        var vm = DataContext as WidgetViewModel;
        var mode = modeOverride ?? vm?.DockMode ?? WidgetDockMode.Floating;
        if (mode == WidgetDockMode.Floating)
        {
            return;
        }

        _isReanchoring = true;
        try
        {
            if (_hwnd == IntPtr.Zero)
            {
                _hwnd = new WindowInteropHelper(this).Handle;
            }

            GetNearestMonitorInfo(out var rcWork, out _);

            var dpi = VisualTreeHelper.GetDpi(this);
            double workAreaDipWidth = (rcWork.Right - rcWork.Left) / dpi.DpiScaleX;

            double targetOuterWidth;
            if (vm != null && vm.IsDockCollapsed)
            {
                targetOuterWidth = 90.0;
            }
            else
            {
                double desiredOuterWidth = MeasureDesiredDockedOuterWidth();
                targetOuterWidth = DockingHelper.CalculateDockedOuterWidth(desiredOuterWidth, workAreaDipWidth);
            }

            if (Math.Abs(Width - targetOuterWidth) > 0.5)
            {
                Width = targetOuterWidth;
            }

            UpdateLayout();

            int physicalWidth = (int)Math.Round(ActualWidth * dpi.DpiScaleX);
            int physicalHeight = (int)Math.Round(ActualHeight * dpi.DpiScaleY);

            if (physicalWidth <= 0)
            {
                physicalWidth = (int)Math.Round(targetOuterWidth * dpi.DpiScaleX);
            }
            if (physicalHeight <= 0)
            {
                physicalHeight = (int)Math.Round((vm != null && vm.IsDockCollapsed ? 7.0 : 40.0) * dpi.DpiScaleY);
            }

            double horizontalAnchor = vm?.DockedHorizontalAnchor ?? 0.5;

            var (physX, physY) = DockingHelper.CalculateDockPosition(
                rcWork.Left,
                rcWork.Top,
                rcWork.Right,
                rcWork.Bottom,
                physicalWidth,
                physicalHeight,
                mode,
                horizontalAnchor);

            if (_hwnd != IntPtr.Zero)
            {
                SetWindowPos(_hwnd, IntPtr.Zero, physX, physY, physicalWidth, physicalHeight, SWP_NOZORDER | SWP_NOACTIVATE);
            }

            Left = physX / dpi.DpiScaleX;
            Top = physY / dpi.DpiScaleY;
        }
        finally
        {
            _isReanchoring = false;
        }
    }

    private void DockedRootBorder_MouseEnter(object sender, MouseEventArgs e)
    {
        _autoHideTimer.Stop();

        if (DataContext is WidgetViewModel vm && vm.IsAutoHideActive && vm.IsDockCollapsed)
        {
            vm.IsDockCollapsed = false;
            ReanchorDockedWindow();
        }
    }

    private void DockedRootBorder_MouseLeave(object sender, MouseEventArgs e)
    {
        if (DataContext is WidgetViewModel vm && vm.IsAutoHideActive && !vm.IsDockCollapsed && !_isDraggingWindow && !IsSettingsOpen && !_isContextMenuOpen)
        {
            _autoHideTimer.Stop();
            _autoHideTimer.Start();
        }
    }

    private void OnAutoHideTimerTick(object? sender, EventArgs e)
    {
        _autoHideTimer.Stop();

        if (DataContext is WidgetViewModel vm && vm.IsAutoHideActive && !vm.IsDockCollapsed && !_isDraggingWindow && !IsSettingsOpen && !_isContextMenuOpen)
        {
            if (!IsMouseOver)
            {
                vm.IsDockCollapsed = true;
                ReanchorDockedWindow();
            }
        }
    }

    public void ForceDockExpanded()
    {
        _autoHideTimer.Stop();
        if (DataContext is WidgetViewModel vm && vm.IsDockCollapsed)
        {
            vm.IsDockCollapsed = false;
            ReanchorDockedWindow();
        }
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

        var vm = DataContext as WidgetViewModel;
        _initialDockMode = vm?.DockMode ?? WidgetDockMode.Floating;

        _autoHideTimer.Stop();
        if (vm != null && vm.IsDockCollapsed)
        {
            vm.IsDockCollapsed = false;
            ReanchorDockedWindow();
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

        var vm = DataContext as WidgetViewModel;
        if (vm == null || _hwnd == IntPtr.Zero)
        {
            return;
        }

        if (!GetWindowRect(_hwnd, out var currentRect) || !GetCursorPos(out var currentCursor))
        {
            return;
        }

        GetNearestMonitorInfo(out var rcWork, out _);
        var dpi = VisualTreeHelper.GetDpi(this);

        var targetMode = DockingHelper.GetDockTargetOnRelease(
            _initialDockMode,
            currentRect.Left,
            currentRect.Top,
            currentRect.Right,
            currentRect.Bottom,
            currentCursor.X,
            currentCursor.Y,
            rcWork.Left,
            rcWork.Top,
            rcWork.Right,
            rcWork.Bottom,
            dpi.DpiScaleX,
            dpi.DpiScaleY);

        if (_initialDockMode == WidgetDockMode.Floating)
        {
            if (targetMode != WidgetDockMode.Floating)
            {
                // Magnetic snap into Top or Bottom dock!
                int physicalWidth = currentRect.Right - currentRect.Left;
                var anchor = DockingHelper.CalculateAnchorFromPhysicalPosition(
                    currentRect.Left,
                    physicalWidth,
                    rcWork.Left,
                    rcWork.Right);

                vm.DockedHorizontalAnchor = anchor;
                vm.DockMode = targetMode;
            }
            else
            {
                // Remained floating
                Left = currentRect.Left / dpi.DpiScaleX;
                Top = currentRect.Top / dpi.DpiScaleY;
                var contentWidth = WidgetContentWidth;
                SizeOrPositionChanged?.Invoke(Left, Top, contentWidth);
            }
        }
        else // _initialDockMode != WidgetDockMode.Floating
        {
            if (targetMode == WidgetDockMode.Floating)
            {
                // Pulled away to undock!
                var restoredContentWidth = ResponsiveLayoutHelper.ClampWidth(vm.WidgetWidth);
                var restoredOuterWidth = restoredContentWidth + 20.0;
                var dropDipLeft = currentRect.Left / dpi.DpiScaleX;
                var dropDipTop = currentRect.Top / dpi.DpiScaleY;
                var safePos = PositionHelper.GetSafePosition(dropDipLeft, dropDipTop, windowWidth: restoredOuterWidth);

                vm.DockMode = WidgetDockMode.Floating;
                Left = safePos.Left;
                Top = safePos.Top;
                Width = restoredOuterWidth;
                SizeOrPositionChanged?.Invoke(safePos.Left, safePos.Top, restoredContentWidth);
            }
            else if (targetMode != _initialDockMode)
            {
                // Direct Top <-> Bottom transition!
                int physicalWidth = currentRect.Right - currentRect.Left;
                var anchor = DockingHelper.CalculateAnchorFromPhysicalPosition(
                    currentRect.Left,
                    physicalWidth,
                    rcWork.Left,
                    rcWork.Right);

                vm.DockedHorizontalAnchor = anchor;
                vm.DockMode = targetMode;
            }
            else
            {
                // Dragged horizontally along the dock edge
                int physicalWidth = currentRect.Right - currentRect.Left;
                var anchor = DockingHelper.CalculateAnchorFromPhysicalPosition(
                    currentRect.Left,
                    physicalWidth,
                    rcWork.Left,
                    rcWork.Right);

                vm.DockedHorizontalAnchor = anchor;
                ReanchorDockedWindow(_initialDockMode);
            }
        }

        if (vm.IsAutoHideActive && !IsMouseOver && !IsSettingsOpen)
        {
            _autoHideTimer.Stop();
            _autoHideTimer.Start();
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

    private void DockedMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (DockedMenuButton.ContextMenu != null)
        {
            DockedMenuButton.ContextMenu.PlacementTarget = DockedMenuButton;
            DockedMenuButton.ContextMenu.Placement = PlacementMode.Bottom;
            DockedMenuButton.ContextMenu.IsOpen = true;
        }
    }

    private void DockedContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        _isContextMenuOpen = true;
        _autoHideTimer.Stop();
    }

    private void DockedContextMenu_Closed(object sender, RoutedEventArgs e)
    {
        _isContextMenuOpen = false;
        if (DataContext is WidgetViewModel vm && vm.IsAutoHideActive && !IsMouseOver && !IsSettingsOpen)
        {
            _autoHideTimer.Stop();
            _autoHideTimer.Start();
        }
    }

    private void DockModeFloating_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WidgetViewModel vm)
        {
            vm.DockMode = WidgetDockMode.Floating;
        }
    }

    private void DockModeTop_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WidgetViewModel vm)
        {
            vm.DockMode = WidgetDockMode.Top;
        }
    }

    private void DockModeBottom_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WidgetViewModel vm)
        {
            vm.DockMode = WidgetDockMode.Bottom;
        }
    }
}
