namespace AIQuotaBar.App;

using System.Windows;
using AIQuotaBar.App.Layout;
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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settingsManager = new SettingsManager();
        _settings = _settingsManager.Load();

        var initialWidth = ResponsiveLayoutHelper.ClampWidth(_settings.WidgetWidth);

        _viewModel = new WidgetViewModel
        {
            IsAlwaysOnTop = _settings.IsAlwaysOnTop,
            IsCompactMode = _settings.IsCompactMode,
            WidgetWidth = initialWidth
        };

        var safePos = PositionHelper.GetSafePosition(_settings.WindowLeft, _settings.WindowTop, windowWidth: initialWidth);

        _window = new WidgetWindow
        {
            DataContext = _viewModel,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Width = initialWidth + 20.0,
            Left = safePos.Left,
            Top = safePos.Top
        };

        // Persist position and width when moved or resized
        _window.SizeOrPositionChanged = (left, top, width) =>
        {
            if (_settings != null && _settingsManager != null)
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
                    _window.Activate();
                }
            },
            exitAction: () => Shutdown(),
            startWithWindowsChanged: enabled =>
            {
                if (_settings != null && _settingsManager != null)
                {
                    _settings.StartWithWindows = enabled;
                    _settingsManager.Save(_settings);
                }
            });

        _viewModel.Start();
        _window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Save current window position and size before shutdown
        if (_window != null && _settings != null && _settingsManager != null && _window.WindowState == WindowState.Normal)
        {
            _settings.WindowLeft = _window.Left;
            _settings.WindowTop = _window.Top;
            _settings.WidgetWidth = _window.WidgetContentWidth;
            _settingsManager.Save(_settings);
        }

        _trayManager?.Dispose();
        _viewModel?.Dispose();

        base.OnExit(e);
    }
}
