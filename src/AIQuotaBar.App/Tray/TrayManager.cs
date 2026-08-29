namespace AIQuotaBar.App.Tray;

using System.Drawing;
using System.Windows.Forms;
using AIQuotaBar.App.Layout;
using AIQuotaBar.App.Settings;
using AIQuotaBar.App.ViewModels;

public sealed class TrayManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly ToolStripMenuItem _showMenuItem;
    private readonly ToolStripMenuItem _statusMenuItem;
    private readonly ToolStripMenuItem _refreshMenuItem;
    private readonly ToolStripMenuItem _settingsMenuItem;
    private readonly ToolStripMenuItem _windowModeMenuItem;
    private readonly ToolStripMenuItem _floatingModeMenuItem;
    private readonly ToolStripMenuItem _dockTopMenuItem;
    private readonly ToolStripMenuItem _dockBottomMenuItem;
    private readonly ToolStripMenuItem _compactModeMenuItem;
    private readonly ToolStripMenuItem _alwaysOnTopMenuItem;
    private readonly ToolStripMenuItem _startWithWindowsMenuItem;
    private readonly ToolStripMenuItem _exitMenuItem;

    private readonly WidgetViewModel _viewModel;
    private readonly Action _showWindowAction;
    private readonly Action _showSettingsAction;
    private readonly Action _exitAction;
    private readonly Action<bool>? _startWithWindowsChanged;
    private readonly Func<bool>? _isNotificationsEnabled;
    private readonly QuotaNotificationEvaluator _notificationEvaluator;

    private Icon? _currentDynamicIcon;
    private bool _disposed;

    public TrayManager(
        WidgetViewModel viewModel,
        Action showWindowAction,
        Action showSettingsAction,
        Action exitAction,
        Action<bool>? startWithWindowsChanged = null,
        Func<bool>? isNotificationsEnabled = null)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _showWindowAction = showWindowAction ?? throw new ArgumentNullException(nameof(showWindowAction));
        _showSettingsAction = showSettingsAction ?? throw new ArgumentNullException(nameof(showSettingsAction));
        _exitAction = exitAction ?? throw new ArgumentNullException(nameof(exitAction));
        _startWithWindowsChanged = startWithWindowsChanged;
        _isNotificationsEnabled = isNotificationsEnabled;
        _notificationEvaluator = new QuotaNotificationEvaluator();

        _contextMenu = new ContextMenuStrip();

        var baseFont = _contextMenu.Font ?? SystemFonts.DefaultFont;
        _showMenuItem = new ToolStripMenuItem("Open AIQuotaBar", null, (s, e) => _showWindowAction())
        {
            Font = new Font(baseFont, FontStyle.Bold)
        };

        _statusMenuItem = new ToolStripMenuItem("Waiting for quota data")
        {
            Enabled = false
        };

        _refreshMenuItem = new ToolStripMenuItem("Refresh", null, (s, e) =>
        {
            if (_viewModel.RefreshCommand.CanExecute(null))
            {
                _viewModel.RefreshCommand.Execute(null);
            }
        });

        _settingsMenuItem = new ToolStripMenuItem("Settings...", null, (s, e) => _showSettingsAction());

        _windowModeMenuItem = new ToolStripMenuItem("Window mode");
        _floatingModeMenuItem = new ToolStripMenuItem("Floating", null, (s, e) => _viewModel.DockMode = WidgetDockMode.Floating)
        {
            Checked = _viewModel.DockMode == WidgetDockMode.Floating
        };
        _dockTopMenuItem = new ToolStripMenuItem("Dock to top", null, (s, e) => _viewModel.DockMode = WidgetDockMode.Top)
        {
            Checked = _viewModel.DockMode == WidgetDockMode.Top
        };
        _dockBottomMenuItem = new ToolStripMenuItem("Dock to bottom", null, (s, e) => _viewModel.DockMode = WidgetDockMode.Bottom)
        {
            Checked = _viewModel.DockMode == WidgetDockMode.Bottom
        };

        _windowModeMenuItem.DropDownItems.Add(_floatingModeMenuItem);
        _windowModeMenuItem.DropDownItems.Add(_dockTopMenuItem);
        _windowModeMenuItem.DropDownItems.Add(_dockBottomMenuItem);

        _compactModeMenuItem = new ToolStripMenuItem("Compact Mode", null, (s, e) =>
        {
            _viewModel.IsCompactMode = !_viewModel.IsCompactMode;
        })
        {
            Checked = _viewModel.IsCompactMode,
            CheckOnClick = true,
            Enabled = _viewModel.IsFloatingMode
        };

        _alwaysOnTopMenuItem = new ToolStripMenuItem("Always on top", null, (s, e) =>
        {
            _viewModel.IsAlwaysOnTop = !_viewModel.IsAlwaysOnTop;
        })
        {
            Checked = _viewModel.IsAlwaysOnTop,
            CheckOnClick = true
        };

        _startWithWindowsMenuItem = new ToolStripMenuItem("Start with Windows", null, async (s, e) =>
        {
            if (s is ToolStripMenuItem item)
            {
                var isChecked = item.Checked;
                var success = await StartupManager.SetStartupAsync(isChecked);
                if (!success)
                {
                    item.Checked = !isChecked;
                }
                else
                {
                    _startWithWindowsChanged?.Invoke(isChecked);
                }
            }
        })
        {
            Checked = StartupManager.IsStartupEnabled(),
            CheckOnClick = true
        };

        _exitMenuItem = new ToolStripMenuItem("Exit", null, (s, e) => _exitAction());

        _contextMenu.Items.Add(_showMenuItem);
        _contextMenu.Items.Add(_statusMenuItem);
        _contextMenu.Items.Add(_refreshMenuItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(_settingsMenuItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(_windowModeMenuItem);
        _contextMenu.Items.Add(_compactModeMenuItem);
        _contextMenu.Items.Add(_alwaysOnTopMenuItem);
        _contextMenu.Items.Add(_startWithWindowsMenuItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(_exitMenuItem);

        _contextMenu.Opening += async (s, e) =>
        {
            var isEnabled = await StartupManager.IsStartupEnabledAsync();
            _startWithWindowsMenuItem.Checked = isEnabled;
        };

        var initialState = TrayHealthCalculator.Calculate(_viewModel.Providers);
        _currentDynamicIcon = TrayIconFactory.CreateIcon(initialState.HealthLevel);
        _statusMenuItem.Text = initialState.StatusMenuText;

        _notifyIcon = new NotifyIcon
        {
            Text = initialState.TooltipText,
            Icon = _currentDynamicIcon,
            ContextMenuStrip = _contextMenu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (s, e) => _showWindowAction();
        _notifyIcon.MouseClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _showWindowAction();
            }
        };
        _notifyIcon.BalloonTipClicked += (s, e) => _showWindowAction();

        // Subscribe to ViewModel property changes to keep tray checkmarks synchronized
        _viewModel.CompactModeChanged += OnCompactModeChanged;
        _viewModel.AlwaysOnTopChanged += OnAlwaysOnTopChanged;
        _viewModel.DockModeChanged += OnDockModeChanged;

        // Event-driven tray updates
        _viewModel.QuotaStateUpdated += OnQuotaStateUpdated;
        _viewModel.VisibilityStateUpdated += OnVisibilityStateUpdated;
        _viewModel.ProviderDiscoveryUpdated += OnProviderDiscoveryUpdated;
    }

    private void OnDockModeChanged(WidgetDockMode mode)
    {
        _floatingModeMenuItem.Checked = mode == WidgetDockMode.Floating;
        _dockTopMenuItem.Checked = mode == WidgetDockMode.Top;
        _dockBottomMenuItem.Checked = mode == WidgetDockMode.Bottom;
        _compactModeMenuItem.Enabled = mode == WidgetDockMode.Floating;
    }

    private void OnCompactModeChanged(bool isCompact)
    {
        _compactModeMenuItem.Checked = isCompact;
    }

    private void OnAlwaysOnTopChanged(bool isTopmost)
    {
        _alwaysOnTopMenuItem.Checked = isTopmost;
    }

    private void OnQuotaStateUpdated()
    {
        if (_disposed)
        {
            return;
        }

        UpdateTrayState(evaluateNotifications: true);
    }

    private void OnVisibilityStateUpdated()
    {
        if (_disposed)
        {
            return;
        }

        UpdateTrayState(evaluateNotifications: false);
    }

    private void OnProviderDiscoveryUpdated()
    {
        if (_disposed)
        {
            return;
        }

        UpdateTrayState(evaluateNotifications: false);
    }

    private void UpdateTrayState(bool evaluateNotifications)
    {
        var state = TrayHealthCalculator.Calculate(_viewModel.Providers);

        // 1. Update tooltip safely
        _notifyIcon.Text = state.TooltipText;

        // 2. Update context menu status line
        _statusMenuItem.Text = state.StatusMenuText;

        // 3. Update dynamic icon with leak-free management
        var newIcon = TrayIconFactory.CreateIcon(state.HealthLevel);
        var oldIcon = _currentDynamicIcon;

        _notifyIcon.Icon = newIcon;
        _currentDynamicIcon = newIcon;

        oldIcon?.Dispose();

        // 4. Extract visible quota observations
        var visibleObservations = _viewModel.Providers
            .Where(p => p.IsVisibleByPreference)
            .SelectMany(p => p.VisibleWindows.Select(w => new QuotaObservation(
                p.ProviderId,
                p.ProviderName,
                w.Id,
                w.RawDisplayName,
                w.RemainingPercent,
                w.Status,
                p.IsQuotaStale)))
            .ToList();

        var isEnabled = _isNotificationsEnabled?.Invoke() ?? true;

        if (evaluateNotifications)
        {
            var notification = _notificationEvaluator.Evaluate(visibleObservations, isEnabled);
            if (notification != null)
            {
                _notifyIcon.ShowBalloonTip(3000, notification.Title, notification.Message, notification.Icon);
            }
        }
        else
        {
            // Visibility change only: synchronize evaluator state silently without firing alerts
            _notificationEvaluator.Evaluate(visibleObservations, notificationsEnabled: false);
        }
    }

    public void UpdateStartWithWindowsChecked(bool enabled)
    {
        _startWithWindowsMenuItem.Checked = enabled;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        _viewModel.CompactModeChanged -= OnCompactModeChanged;
        _viewModel.AlwaysOnTopChanged -= OnAlwaysOnTopChanged;
        _viewModel.DockModeChanged -= OnDockModeChanged;
        _viewModel.QuotaStateUpdated -= OnQuotaStateUpdated;
        _viewModel.VisibilityStateUpdated -= OnVisibilityStateUpdated;
        _viewModel.ProviderDiscoveryUpdated -= OnProviderDiscoveryUpdated;

        _notifyIcon.Visible = false;
        _notifyIcon.Icon = null;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();

        _currentDynamicIcon?.Dispose();
        _currentDynamicIcon = null;
    }
}
