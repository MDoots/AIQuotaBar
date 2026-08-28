namespace AIQuotaBar.App.ViewModels;

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AIQuotaBar.App.Layout;
using AIQuotaBar.App.Settings;
using AIQuotaBar.Core.Interfaces;
using AIQuotaBar.Core.Models;
using AIQuotaBar.Providers.Antigravity;
using AIQuotaBar.Providers.Codex;

public sealed class WidgetViewModel : ViewModelBase, IDisposable
{
    private readonly List<DispatcherTimer> _providerTimers = new();
    private readonly DispatcherTimer _countdownTimer;
    private bool _disposed;

    private bool _isAlwaysOnTop = true;
    private bool _isCompactMode;
    private double _widgetWidth = ResponsiveLayoutHelper.DefaultWidgetWidth;
    private WidgetLayoutMode _layoutMode = WidgetLayoutMode.Full;
    private string _lastUpdatedText = "Not updated yet";
    private AppSettings? _appSettings;

    public Action<bool>? AlwaysOnTopChanged { get; set; }
    public Action<bool>? CompactModeChanged { get; set; }
    public Action<double>? WidgetWidthChanged { get; set; }

    public event Action? QuotaStateUpdated;
    public event Action? VisibilityStateUpdated;

    public ObservableCollection<ProviderSectionViewModel> Providers { get; } = new();
    public ObservableCollection<ProviderSectionViewModel> VisibleProviders { get; } = new();

    public bool ShowEmptyState => VisibleProviders.Count == 0;

    public double WidgetWidth
    {
        get => _widgetWidth;
        set
        {
            var clamped = ResponsiveLayoutHelper.ClampWidth(value);
            if (SetProperty(ref _widgetWidth, clamped))
            {
                LayoutMode = ResponsiveLayoutHelper.GetLayoutMode(clamped);
                WidgetWidthChanged?.Invoke(clamped);
            }
        }
    }

    public WidgetLayoutMode LayoutMode
    {
        get => _layoutMode;
        private set
        {
            if (SetProperty(ref _layoutMode, value))
            {
                OnPropertyChanged(nameof(ShowFooter));
                OnPropertyChanged(nameof(AppTitleText));
                OnPropertyChanged(nameof(ShowAppTitle));
                OnPropertyChanged(nameof(ShowModeToggle));
                OnPropertyChanged(nameof(ShowSettingsButton));
                OnPropertyChanged(nameof(MainCardMargin));
                foreach (var provider in Providers)
                {
                    provider.LayoutMode = value;
                }
            }
        }
    }

    public string AppTitleText => "AIQuotaBar";
    public bool ShowAppTitle => LayoutMode != WidgetLayoutMode.Micro;
    public bool ShowModeToggle => LayoutMode != WidgetLayoutMode.Micro;
    public bool ShowSettingsButton => LayoutMode is WidgetLayoutMode.Full or WidgetLayoutMode.Compact;
    public Thickness MainCardMargin => LayoutMode == WidgetLayoutMode.Micro ? new Thickness(6, 6, 6, 6) : new Thickness(10, 8, 10, 8);

    public bool ShowFooter => !IsCompactMode && LayoutMode != WidgetLayoutMode.Micro;

    public bool IsAlwaysOnTop
    {
        get => _isAlwaysOnTop;
        set
        {
            if (SetProperty(ref _isAlwaysOnTop, value))
            {
                AlwaysOnTopChanged?.Invoke(value);
            }
        }
    }

    public bool IsCompactMode
    {
        get => _isCompactMode;
        set
        {
            if (SetProperty(ref _isCompactMode, value))
            {
                OnPropertyChanged(nameof(ModeToggleText));
                OnPropertyChanged(nameof(ModeToggleTooltip));
                OnPropertyChanged(nameof(ShowFooter));
                foreach (var provider in Providers)
                {
                    provider.IsCompactMode = value;
                }
                CompactModeChanged?.Invoke(value);
            }
        }
    }

    public string ModeToggleText => IsCompactMode ? "▾" : "▴";
    public string ModeToggleTooltip => IsCompactMode ? "Switch to Expanded View" : "Switch to Compact View";

    public bool IsLoading => Providers.Any(p => p.IsLoading);

    public string LastUpdatedText
    {
        get => _lastUpdatedText;
        private set => SetProperty(ref _lastUpdatedText, value);
    }

    public bool CanRefresh => !IsLoading && !_disposed;

    public ICommand ToggleModeCommand { get; }
    public ICommand RefreshCommand { get; }

    public WidgetViewModel(IEnumerable<ProviderSectionViewModel>? providerSections = null)
    {
        ToggleModeCommand = new RelayCommand(() => IsCompactMode = !IsCompactMode);
        RefreshCommand = new RelayCommand(async () => await RefreshAllAsync(), () => CanRefresh);

        if (providerSections != null)
        {
            foreach (var section in providerSections)
            {
                Providers.Add(section);
            }
        }
        else
        {
            // Default production configuration:
            // 1. OpenAI Codex: 60s auto-refresh
            // 2. Google Antigravity: 180s auto-refresh
            Providers.Add(new ProviderSectionViewModel(new CodexUsageProvider(), TimeSpan.FromSeconds(60)));
            Providers.Add(new ProviderSectionViewModel(new AntigravityUsageProvider(), TimeSpan.FromSeconds(180)));
        }

        // Set up individual auto-refresh timers for each provider
        foreach (var provider in Providers)
        {
            provider.LayoutMode = _layoutMode;
            provider.IsCompactMode = _isCompactMode;
            provider.PropertyChanged += OnProviderPropertyChanged;
            provider.SnapshotApplied += OnProviderSnapshotApplied;

            var timer = new DispatcherTimer
            {
                Interval = provider.RefreshInterval
            };
            timer.Tick += async (s, e) =>
            {
                if (!_disposed && !provider.IsLoading)
                {
                    await provider.RefreshAsync().ConfigureAwait(true);
                    UpdateLastUpdated();
                }
            };
            _providerTimers.Add(timer);
        }

        UpdateVisibility(_appSettings);

        // Local countdown display refresh every 30 seconds (no process spawn)
        _countdownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _countdownTimer.Tick += OnCountdownTimerTick;
    }

    public WidgetViewModel(IUsageProvider provider, TimeSpan? refreshInterval = null)
        : this(new[] { new ProviderSectionViewModel(provider, refreshInterval ?? TimeSpan.FromSeconds(60)) })
    {
    }

    private int _batchRefreshCount;

    private void OnProviderSnapshotApplied()
    {
        if (_disposed)
        {
            return;
        }

        UpdateLastUpdated();
        if (_batchRefreshCount == 0)
        {
            QuotaStateUpdated?.Invoke();
        }
    }

    private void OnProviderPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProviderSectionViewModel.ShouldDisplayInWidget)
            or nameof(ProviderSectionViewModel.HasVisibleWindows)
            or nameof(ProviderSectionViewModel.IsLoading)
            or nameof(ProviderSectionViewModel.HasStatusMessage)
            or nameof(ProviderSectionViewModel.IsVisibleByPreference))
        {
            UpdateVisibleProvidersCollection();
        }
    }

    public void UpdateVisibility(AppSettings? settings = null)
    {
        if (settings != null)
        {
            _appSettings = settings;
        }

        foreach (var provider in Providers)
        {
            provider.ApplyVisibilityFilter(_appSettings);
        }

        UpdateVisibleProvidersCollection();
        VisibilityStateUpdated?.Invoke();
    }

    private void UpdateVisibleProvidersCollection()
    {
        void Update()
        {
            VisibleProviders.Clear();
            foreach (var provider in Providers)
            {
                if (provider.ShouldDisplayInWidget)
                {
                    VisibleProviders.Add(provider);
                }
            }

            OnPropertyChanged(nameof(ShowEmptyState));
        }

        if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.Invoke(Update);
        }
        else
        {
            Update();
        }
    }

    public void Start()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var timer in _providerTimers)
        {
            timer.Start();
        }

        _countdownTimer.Start();
        _ = RefreshAllAsync();
    }

    public async Task RefreshAllAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        Interlocked.Increment(ref _batchRefreshCount);
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(CanRefresh));

        try
        {
            var refreshTasks = Providers.Select(p => p.RefreshAsync(cancellationToken)).ToArray();
            await Task.WhenAll(refreshTasks).ConfigureAwait(true);
        }
        finally
        {
            Interlocked.Decrement(ref _batchRefreshCount);
            UpdateLastUpdated();
            OnPropertyChanged(nameof(IsLoading));
            OnPropertyChanged(nameof(CanRefresh));
            QuotaStateUpdated?.Invoke();
        }
    }

    private void OnCountdownTimerTick(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        foreach (var provider in Providers)
        {
            provider.RefreshCountdown();
        }
    }

    private void UpdateLastUpdated()
    {
        var latest = Providers
            .Where(p => p.LastRefreshedAt.HasValue)
            .Select(p => p.LastRefreshedAt!.Value)
            .DefaultIfEmpty()
            .Max();

        if (latest != default)
        {
            LastUpdatedText = $"Updated {latest.ToLocalTime():HH:mm:ss}";
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        foreach (var timer in _providerTimers)
        {
            timer.Stop();
        }
        _providerTimers.Clear();

        _countdownTimer.Stop();
        _countdownTimer.Tick -= OnCountdownTimerTick;

        foreach (var provider in Providers)
        {
            provider.PropertyChanged -= OnProviderPropertyChanged;
            provider.SnapshotApplied -= OnProviderSnapshotApplied;
            provider.Dispose();
        }

        OnPropertyChanged(nameof(CanRefresh));
    }
}
