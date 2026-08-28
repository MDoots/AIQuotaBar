namespace AIQuotaBar.App.Tray;

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AIQuotaBar.App.Settings;
using AIQuotaBar.App.ViewModels;

public sealed class TrayManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly ToolStripMenuItem _showMenuItem;
    private readonly ToolStripMenuItem _refreshMenuItem;
    private readonly ToolStripMenuItem _settingsMenuItem;
    private readonly ToolStripMenuItem _compactModeMenuItem;
    private readonly ToolStripMenuItem _alwaysOnTopMenuItem;
    private readonly ToolStripMenuItem _startWithWindowsMenuItem;
    private readonly ToolStripMenuItem _exitMenuItem;

    private readonly WidgetViewModel _viewModel;
    private readonly Action _showWindowAction;
    private readonly Action _showSettingsAction;
    private readonly Action _exitAction;
    private readonly Action<bool>? _startWithWindowsChanged;
    private bool _disposed;

    public TrayManager(
        WidgetViewModel viewModel,
        Action showWindowAction,
        Action showSettingsAction,
        Action exitAction,
        Action<bool>? startWithWindowsChanged = null)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _showWindowAction = showWindowAction ?? throw new ArgumentNullException(nameof(showWindowAction));
        _showSettingsAction = showSettingsAction ?? throw new ArgumentNullException(nameof(showSettingsAction));
        _exitAction = exitAction ?? throw new ArgumentNullException(nameof(exitAction));
        _startWithWindowsChanged = startWithWindowsChanged;

        _contextMenu = new ContextMenuStrip();

        var baseFont = _contextMenu.Font ?? SystemFonts.DefaultFont;
        _showMenuItem = new ToolStripMenuItem("Show AIQuotaBar", null, (s, e) => _showWindowAction())
        {
            Font = new Font(baseFont, FontStyle.Bold)
        };

        _refreshMenuItem = new ToolStripMenuItem("Refresh", null, (s, e) =>
        {
            if (_viewModel.RefreshCommand.CanExecute(null))
            {
                _viewModel.RefreshCommand.Execute(null);
            }
        });

        _settingsMenuItem = new ToolStripMenuItem("Settings...", null, (s, e) => _showSettingsAction());

        _compactModeMenuItem = new ToolStripMenuItem("Compact Mode", null, (s, e) =>
        {
            _viewModel.IsCompactMode = !_viewModel.IsCompactMode;
        })
        {
            Checked = _viewModel.IsCompactMode,
            CheckOnClick = true
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
        _contextMenu.Items.Add(_refreshMenuItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(_settingsMenuItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
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

        _notifyIcon = new NotifyIcon
        {
            Text = "AIQuotaBar",
            Icon = CreateDefaultIcon(),
            ContextMenuStrip = _contextMenu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (s, e) => _showWindowAction();

        // Subscribe to ViewModel property changes to keep tray checkmarks synchronized
        _viewModel.CompactModeChanged += isCompact => _compactModeMenuItem.Checked = isCompact;
        _viewModel.AlwaysOnTopChanged += isTopmost => _alwaysOnTopMenuItem.Checked = isTopmost;
    }

    public void UpdateStartWithWindowsChecked(bool enabled)
    {
        _startWithWindowsMenuItem.Checked = enabled;
    }

    private static Icon CreateDefaultIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // Dark rounded background
            using var bgBrush = new SolidBrush(Color.FromArgb(24, 24, 27));
            using var borderPen = new Pen(Color.FromArgb(63, 63, 70), 1.5f);
            
            var rect = new Rectangle(1, 1, 29, 29);
            g.FillEllipse(bgBrush, rect);
            g.DrawEllipse(borderPen, rect);

            // Three horizontal quota level bars (emerald, cyan, amber)
            using var barBrush1 = new SolidBrush(Color.FromArgb(16, 185, 129)); // Emerald
            using var barBrush2 = new SolidBrush(Color.FromArgb(56, 189, 248)); // Cyan
            using var barBrush3 = new SolidBrush(Color.FromArgb(245, 158, 11)); // Amber

            g.FillRoundedRectangle(barBrush1, new Rectangle(6, 8, 19, 3), 1);
            g.FillRoundedRectangle(barBrush2, new Rectangle(6, 14, 15, 3), 1);
            g.FillRoundedRectangle(barBrush3, new Rectangle(6, 20, 11, 3), 1);
        }

        var hIcon = bitmap.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
    }
}

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics g, Brush brush, Rectangle bounds, int radius)
    {
        using var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, radius * 2, radius * 2, 180, 90);
        path.AddArc(bounds.Right - radius * 2, bounds.Y, radius * 2, radius * 2, 270, 90);
        path.AddArc(bounds.Right - radius * 2, bounds.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }
}
