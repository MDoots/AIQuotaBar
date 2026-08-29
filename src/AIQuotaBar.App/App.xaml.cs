namespace AIQuotaBar.App;

using System.IO;
using System.Windows;
using System.Windows.Interop;
using AIQuotaBar.App.Layout;
using AIQuotaBar.App.Services;
using AIQuotaBar.App.Settings;
using AIQuotaBar.App.Tray;
using AIQuotaBar.App.ViewModels;
using AIQuotaBar.App.Views;

public partial class App : Application
{
    private SettingsManager? _settingsManager;
    private AppSettings? _settings;
    private WidgetViewModel? _viewModel;
    private WidgetWindow? _window;
    private TrayManager? _trayManager;
    private PowerResumeCoordinator? _powerResumeCoordinator;
    private WidgetDockMode _appliedDockMode = WidgetDockMode.Floating;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settingsManager = new SettingsManager();
        var isFirstRun = !_settingsManager.SettingsFileExists;
        _settings = _settingsManager.Load();

        var initialContentWidth = ResponsiveLayoutHelper.ClampWidth(_settings.WidgetWidth);
        var outerWindowWidth = initialContentWidth + 20.0;
        var initialDockMode = isFirstRun ? WidgetDockMode.Floating : _settings.DockMode;
        _appliedDockMode = initialDockMode;

        _viewModel = new WidgetViewModel
        {
            IsAlwaysOnTop = _settings.IsAlwaysOnTop,
            IsCompactMode = _settings.IsCompactMode,
            WidgetWidth = initialContentWidth,
            DockMode = initialDockMode,
            DockedHorizontalAnchor = _settings.DockedHorizontalAnchor,
            AutoHideDockedBar = _settings.AutoHideDockedBar,
            OpenSettingsRequested = ShowSettingsWindow
        };
        _viewModel.UpdateVisibility(_settings);

        double initialLeft;
        double initialTop;

        if (isFirstRun)
        {
            // First run: explicitly centred in the primary monitor working area
            var centeredPos = PositionHelper.GetCenteredPosition(windowWidth: outerWindowWidth, windowHeight: 160);
            initialLeft = centeredPos.Left;
            initialTop = centeredPos.Top;
        }
        else if (initialDockMode == WidgetDockMode.Floating)
        {
            // Returning Floating user: uses safe position fallback
            var safePos = PositionHelper.GetSafePosition(_settings.WindowLeft, _settings.WindowTop, windowWidth: outerWindowWidth);
            initialLeft = safePos.Left;
            initialTop = safePos.Top;
        }
        else
        {
            // Returning Docked user: use saved raw WPF floating coordinates directly to establish monitor affinity without PositionHelper clamping
            var hostPos = DockingHelper.ResolveInitialDockedHostPosition(_settings.WindowLeft, _settings.WindowTop);
            initialLeft = hostPos.Left;
            initialTop = hostPos.Top;
        }

        _window = new WidgetWindow
        {
            DataContext = _viewModel,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Width = outerWindowWidth,
            Left = initialLeft,
            Top = initialTop,
            OpenSettingsRequested = ShowSettingsWindow
        };

        // Create HWND at initial host coordinates so MonitorFromWindow accurately resolves the target monitor
        new WindowInteropHelper(_window).EnsureHandle();

        if (isFirstRun)
        {
            _window.EnableFirstRunAutoCentering();
        }

        if (initialDockMode != WidgetDockMode.Floating)
        {
            _window.ApplyDockMode(initialDockMode);
        }

        // Persist position and width when moved or resized (Floating mode only)
        _window.SizeOrPositionChanged = (left, top, width) =>
        {
            if (_settings != null && _settingsManager != null && _appliedDockMode == WidgetDockMode.Floating)
            {
                _settings.WindowLeft = left;
                _settings.WindowTop = top;
                _settings.WidgetWidth = width;
                _settingsManager.Save(_settings);
            }
        };

        // Persist settings when changed
        _viewModel.AlwaysOnTopChanged = isTopmost =>
        {
            if (_settings != null && _settingsManager != null)
            {
                _settings.IsAlwaysOnTop = isTopmost;
                _settingsManager.Save(_settings);
            }
        };

        _viewModel.CompactModeChanged = isCompact =>
        {
            if (_settings != null && _settingsManager != null)
            {
                _settings.IsCompactMode = isCompact;
                _settingsManager.Save(_settings);
            }
        };

        _viewModel.DockedHorizontalAnchorChanged = anchor =>
        {
            if (_settings != null && _settingsManager != null)
            {
                _settings.DockedHorizontalAnchor = anchor;
                _settingsManager.Save(_settings);
            }
        };

        _viewModel.AutoHideDockedBarChanged = autoHide =>
        {
            if (_settings != null && _settingsManager != null)
            {
                _settings.AutoHideDockedBar = autoHide;
                _settingsManager.Save(_settings);
            }
        };

        _viewModel.DockModeChanged = mode =>
        {
            if (_settings != null && _settingsManager != null && _window != null)
            {
                var previousMode = _appliedDockMode;
                _appliedDockMode = mode;

                if (mode != WidgetDockMode.Floating)
                {
                    // If transitioning FROM Floating mode, capture the floating geometry BEFORE applying dock changes
                    if (previousMode == WidgetDockMode.Floating)
                    {
                        _settings.WindowLeft = _window.Left;
                        _settings.WindowTop = _window.Top;
                        _settings.WidgetWidth = _window.WidgetContentWidth;
                    }

                    _settings.DockMode = mode;
                    _settings.DockedHorizontalAnchor = _viewModel.DockedHorizontalAnchor;
                    _settingsManager.Save(_settings);

                    _window.ApplyDockMode(mode);
                }
                else
                {
                    // Returning to floating: restore saved floating geometry
                    _settings.DockMode = WidgetDockMode.Floating;
                    _settingsManager.Save(_settings);

                    _window.ApplyDockMode(
                        WidgetDockMode.Floating,
                        restoreFloatingLeft: _settings.WindowLeft,
                        restoreFloatingTop: _settings.WindowTop,
                        restoreFloatingWidth: _settings.WidgetWidth);
                }
            }
        };

        // Initialize system tray icon
        _trayManager = new TrayManager(
            _viewModel,
            showWindowAction: () =>
            {
                if (_window != null)
                {
                    _window.Show();
                    if (_window.WindowState == WindowState.Minimized)
                    {
                        _window.WindowState = WindowState.Normal;
                    }
                    if (_appliedDockMode != WidgetDockMode.Floating)
                    {
                        _window.ForceDockExpanded();
                        _window.ReanchorDockedWindow();
                    }
                    _window.Activate();
                }
            },
            showSettingsAction: ShowSettingsWindow,
            exitAction: () => Shutdown(),
            startWithWindowsChanged: enabled =>
            {
                if (_settings != null && _settingsManager != null)
                {
                    _settings.StartWithWindows = enabled;
                    _settingsManager.Save(_settings);
                }
            },
            isNotificationsEnabled: () => _settings?.LowQuotaNotificationsEnabled ?? true);

        _powerResumeCoordinator = new PowerResumeCoordinator(
            refreshAction: () => _viewModel != null ? _viewModel.RefreshAllAsync() : Task.CompletedTask,
            dispatcher: Dispatcher,
            resumeDelay: TimeSpan.FromSeconds(8));
        _powerResumeCoordinator.Start();

        _viewModel.Start();

        if (isFirstRun)
        {
            _window.Measure(new System.Windows.Size(outerWindowWidth, double.PositiveInfinity));
            _window.Arrange(new System.Windows.Rect(0, 0, outerWindowWidth, _window.DesiredSize.Height));
            _window.UpdateLayout();
            _window.RecenterInPrimaryWorkingArea();
        }

        _window.WindowState = WindowState.Normal;
        _window.Show();
        _window.Activate();
    }

    private SettingsWindow? _settingsWindow;

    private void ShowSettingsWindow()
    {
        if (_settings == null || _settingsManager == null || _viewModel == null)
        {
            return;
        }

        if (_settingsWindow == null || !_settingsWindow.IsLoaded)
        {
            if (_window != null)
            {
                _window.IsSettingsOpen = true;
                _window.ForceDockExpanded();
            }

            var settingsVm = new SettingsViewModel(_settings, _settingsManager, _viewModel);
            _settingsWindow = new SettingsWindow
            {
                DataContext = settingsVm,
                Owner = _window
            };
            _settingsWindow.Closed += (s, e) =>
            {
                if (_window != null)
                {
                    _window.IsSettingsOpen = false;
                }
                settingsVm.Dispose();
                _settingsWindow = null;
            };
            _settingsWindow.Show();
        }
        else
        {
            if (_settingsWindow.WindowState == WindowState.Minimized)
            {
                _settingsWindow.WindowState = WindowState.Normal;
            }
            _settingsWindow.Activate();
            _settingsWindow.Focus();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_settings != null && _settingsManager != null)
        {
            // Only overwrite floating Left/Top/Width if currently in Floating mode!
            if (_appliedDockMode == WidgetDockMode.Floating && _window != null && _window.WindowState == WindowState.Normal)
            {
                _settings.WindowLeft = _window.Left;
                _settings.WindowTop = _window.Top;
                _settings.WidgetWidth = _window.WidgetContentWidth;
            }

            _settings.DockMode = _appliedDockMode;
            if (_viewModel != null)
            {
                _settings.DockedHorizontalAnchor = _viewModel.DockedHorizontalAnchor;
                _settings.AutoHideDockedBar = _viewModel.AutoHideDockedBar;
            }
            _settingsManager.Save(_settings);
        }

        _powerResumeCoordinator?.Dispose();
        _trayManager?.Dispose();
        _viewModel?.Dispose();

        base.OnExit(e);
    }
}
