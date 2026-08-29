namespace AIQuotaBar.App.ViewModels;

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AIQuotaBar.App.Layout;
using AIQuotaBar.App.Providers;
using AIQuotaBar.App.Settings;
using AIQuotaBar.Core.Interfaces;
using AIQuotaBar.Core.Models;

public sealed class WidgetViewModel : ViewModelBase, IDisposable
{
    private readonly List<DispatcherTimer> _providerTimers = new();
    private readonly DispatcherTimer _countdownTimer;
    private readonly IReadOnlyList<ProviderDescriptor> _descriptors;
    private readonly IProviderDiscoveryService _discoveryService;
    private CancellationTokenSource? _discoveryCts;
    private int _discoveryScanGeneration;
    private bool _disposed;

    private bool _isAlwaysOnTop = true;
    private bool _isCompactMode;
    private bool _isDiscoveringProviders;
    private WidgetDockMode _dockMode = WidgetDockMode.Floating;
    private double _widgetWidth = ResponsiveLayoutHelper.DefaultWidgetWidth;
    private WidgetLayoutMode _layoutMode = WidgetLayoutMode.Full;
    private string _lastUpdatedText = "Not updated yet";
    private AppSettings? _appSettings;

    public Action<bool>? AlwaysOnTopChanged { get; set; }
    public Action<bool>? CompactModeChanged { get; set; }
    public Action? OpenSettingsRequested { get; set; }
    private double _dockedHorizontalAnchor = 0.5;
    private bool _autoHideDockedBar = true;
    private bool _isDockCollapsed;

    public Action<double>? WidgetWidthChanged { get; set; }
    public Action<WidgetDockMode>? DockModeChanged { get; set; }
    public Action<double>? DockedHorizontalAnchorChanged { get; set; }
    public Action<bool>? AutoHideDockedBarChanged { get; set; }

    public event Action? QuotaStateUpdated;
    public event Action? VisibilityStateUpdated;
    public event Action? ProviderDiscoveryUpdated;
    public event Action? DockCollapseStateChanged;
    public event Action? InitialStartupSettled;

    public IReadOnlyList<ProviderDescriptor> Descriptors => _descriptors;

    public bool IsDiscoveringProviders
    {
        get => _isDiscoveringProviders;
        private set
        {
            if (SetProperty(ref _isDiscoveringProviders, value))
            {
                OnPropertyChanged(nameof(ShowCheckingProviders));
                OnPropertyChanged(nameof(ShowZeroProvidersDetected));
                OnPropertyChanged(nameof(ShowDiscoveryError));
                OnPropertyChanged(nameof(ShowNoQuotaRowsSelected));
                OnPropertyChanged(nameof(ShowEmptyState));
            }
        }
    }

    public bool HasAnyDetectedProviders => Providers.Any(p => p.DiscoveryStatus == ProviderDiscoveryStatus.Detected);
    public bool HasAnyDiscoveryEligibleProviders => Providers.Any(p => p.DiscoveryStatus != ProviderDiscoveryStatus.NotDetected);

    public bool ShowZeroProvidersDetected => !IsDiscoveringProviders && Providers.Count > 0 && Providers.All(p => p.DiscoveryStatus == ProviderDiscoveryStatus.NotDetected);

    public bool ShowCheckingProviders => IsDiscoveringProviders && !Providers.Any(p => p.HasWindows || p.HasStatusMessage);

    public bool ShowDiscoveryError => !IsDiscoveringProviders &&
                                      !ShowZeroProvidersDetected &&
                                      !ShowCheckingProviders &&
                                      VisibleProviders.Count == 0 &&
                                      Providers.Any(p => p.DiscoveryStatus == ProviderDiscoveryStatus.Error);

    public bool ShowNoQuotaRowsSelected => !IsDiscoveringProviders &&
                                           !ShowZeroProvidersDetected &&
                                           !ShowCheckingProviders &&
                                           !ShowDiscoveryError &&
                                           VisibleProviders.Count == 0 &&
                                           HasAnyDetectedProviders;

    public WidgetDockMode DockMode
    {
        get => _dockMode;
        set
        {
            if (SetProperty(ref _dockMode, value))
            {
                OnPropertyChanged(nameof(IsFloatingMode));
                OnPropertyChanged(nameof(IsDockedMode));
                OnPropertyChanged(nameof(IsDockedTop));
                OnPropertyChanged(nameof(IsDockedBottom));
                OnPropertyChanged(nameof(IsAutoHideActive));
                OnPropertyChanged(nameof(DockedRootCornerRadius));
                OnPropertyChanged(nameof(DockedHandleCornerRadius));
                OnPropertyChanged(nameof(DockedHandleVerticalAlignment));
                DockModeChanged?.Invoke(value);
            }
        }
    }

    public double DockedHorizontalAnchor
    {
        get => _dockedHorizontalAnchor;
        set
        {
            var clamped = Math.Clamp(double.IsNaN(value) || double.IsInfinity(value) ? 0.5 : value, 0.0, 1.0);
            if (SetProperty(ref _dockedHorizontalAnchor, clamped))
            {
                DockedHorizontalAnchorChanged?.Invoke(clamped);
            }
        }
    }

    public bool AutoHideDockedBar
    {
        get => _autoHideDockedBar;
        set
        {
            if (SetProperty(ref _autoHideDockedBar, value))
            {
                OnPropertyChanged(nameof(IsAutoHideActive));
                AutoHideDockedBarChanged?.Invoke(value);
            }
        }
    }

    public bool IsAutoHideActive => IsDockedMode && AutoHideDockedBar;

    public bool IsDockCollapsed
    {
        get => _isDockCollapsed;
        set
        {
            if (SetProperty(ref _isDockCollapsed, value))
            {
                OnPropertyChanged(nameof(IsDockExpanded));
                OnPropertyChanged(nameof(DockedRootMargin));
                OnPropertyChanged(nameof(DockedRootCornerRadius));
                DockCollapseStateChanged?.Invoke();
            }
        }
    }

    public bool IsDockExpanded => !IsDockCollapsed;

    public Thickness DockedRootMargin => new Thickness(0);

    public CornerRadius DockedRootCornerRadius
    {
        get
        {
            if (IsDockCollapsed)
            {
                return IsDockedTop ? new CornerRadius(0, 0, 4, 4) : new CornerRadius(4, 4, 0, 0);
            }
            return IsDockedTop ? new CornerRadius(0, 0, 8, 8) : new CornerRadius(8, 8, 0, 0);
        }
    }

    public CornerRadius DockedHandleCornerRadius => IsDockedTop ? new CornerRadius(0, 0, 4, 4) : new CornerRadius(4, 4, 0, 0);

    public VerticalAlignment DockedHandleVerticalAlignment => IsDockedTop ? VerticalAlignment.Top : VerticalAlignment.Bottom;

    public bool IsFloatingMode => DockMode == WidgetDockMode.Floating;
    public bool IsDockedMode => DockMode != WidgetDockMode.Floating;
    public bool IsDockedTop => DockMode == WidgetDockMode.Top;
    public bool IsDockedBottom => DockMode == WidgetDockMode.Bottom;

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
    public ICommand OpenSettingsCommand { get; }

    public WidgetViewModel(
        IReadOnlyList<ProviderDescriptor>? descriptors = null,
        IProviderDiscoveryService? discoveryService = null,
        IEnumerable<ProviderSectionViewModel>? providerSections = null)
    {
        _descriptors = descriptors ?? (providerSections == null ? ProviderCatalog.All : Array.Empty<ProviderDescriptor>());
        _discoveryService = discoveryService ?? new ProviderDiscoveryService();
        _isDiscoveringProviders = false;

        ToggleModeCommand = new RelayCommand(() => IsCompactMode = !IsCompactMode);
        RefreshCommand = new RelayCommand(async () => await RefreshAllAsync(), () => CanRefresh);
        OpenSettingsCommand = new RelayCommand(() => OpenSettingsRequested?.Invoke());

        if (providerSections != null)
        {
            foreach (var section in providerSections)
            {
                Providers.Add(section);
            }
        }
        else
        {
            foreach (var descriptor in _descriptors)
            {
                Providers.Add(new ProviderSectionViewModel(
                    descriptor.CreateProvider(),
                    descriptor.RefreshInterval,
                    descriptor.ShortDisplayName,
                    descriptor.AccentColor,
                    ProviderDiscoveryStatus.Unknown));
            }
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
                if (!_disposed && !provider.IsLoading &&
                    provider.DiscoveryStatus != ProviderDiscoveryStatus.NotDetected &&
                    provider.DiscoveryStatus != ProviderDiscoveryStatus.Checking &&
                    provider.DiscoveryStatus != ProviderDiscoveryStatus.Unknown)
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

    public WidgetViewModel(IEnumerable<ProviderSectionViewModel>? providerSections)
        : this(null, null, providerSections)
    {
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
            or nameof(ProviderSectionViewModel.IsVisibleByPreference)
            or nameof(ProviderSectionViewModel.DiscoveryStatus)
            or nameof(ProviderSectionViewModel.IsProviderDetected)
            or nameof(ProviderSectionViewModel.IsProviderNotDetected))
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
            OnPropertyChanged(nameof(ShowZeroProvidersDetected));
            OnPropertyChanged(nameof(ShowCheckingProviders));
            OnPropertyChanged(nameof(ShowDiscoveryError));
            OnPropertyChanged(nameof(ShowNoQuotaRowsSelected));
            OnPropertyChanged(nameof(HasAnyDetectedProviders));
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

        if (_descriptors.Count > 0)
        {
            BeginProviderDiscovery(isStartup: true);
        }
        else
        {
            _ = RefreshAllAsync();
        }
    }

    public void BeginProviderDiscovery(bool isStartup = false)
    {
        if (_disposed || _descriptors.Count == 0)
        {
            return;
        }

        IsDiscoveringProviders = true;
        foreach (var provider in Providers)
        {
            if (provider.DiscoveryStatus == ProviderDiscoveryStatus.Unknown)
            {
                provider.ApplyDiscoveryStatus(ProviderDiscoveryStatus.Checking);
            }
        }
        UpdateVisibleProvidersCollection();

        _ = DiscoverProvidersAsync(isStartup);
    }

    public async Task DiscoverProvidersAsync(bool isStartup = false, CancellationToken cancellationToken = default)
    {
        if (_disposed || _descriptors.Count == 0)
        {
            return;
        }

        _discoveryCts?.Cancel();
        _discoveryCts?.Dispose();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _discoveryCts = cts;
        var currentGeneration = Interlocked.Increment(ref _discoveryScanGeneration);

        IsDiscoveringProviders = true;
        try
        {
            var results = await _discoveryService.DiscoverAsync(_descriptors, cts.Token).ConfigureAwait(true);
            if (_disposed || cts.IsCancellationRequested || currentGeneration != _discoveryScanGeneration)
            {
                return;
            }

            var providersToRefresh = new List<ProviderSectionViewModel>();

            foreach (var result in results)
            {
                var provider = Providers.FirstOrDefault(p => string.Equals(p.ProviderId, result.ProviderId, StringComparison.OrdinalIgnoreCase));
                if (provider != null)
                {
                    var previousStatus = provider.DiscoveryStatus;
                    provider.ApplyDiscoveryStatus(result.Status);

                    if (result.Status == ProviderDiscoveryStatus.Detected)
                    {
                        var isConnectedHealthy = provider.Status == ProviderStatus.Available && provider.HasWindows && provider.LastRefreshedAt != null;
                        var needsRefresh = isStartup ||
                                           previousStatus == ProviderDiscoveryStatus.NotDetected ||
                                           provider.LastRefreshedAt == null ||
                                           provider.Status is ProviderStatus.Unauthenticated
                                                           or ProviderStatus.Error
                                                           or ProviderStatus.Timeout
                                                           or ProviderStatus.Unavailable ||
                                           !isConnectedHealthy;

                        if (needsRefresh)
                        {
                            providersToRefresh.Add(provider);
                        }
                    }
                }
            }

            if (_disposed || cts.IsCancellationRequested || currentGeneration != _discoveryScanGeneration)
            {
                return;
            }

            UpdateVisibleProvidersCollection();
            ProviderDiscoveryUpdated?.Invoke();

            if (providersToRefresh.Count > 0)
            {
                await Task.WhenAll(providersToRefresh.Select(p => p.RefreshAsync(cts.Token))).ConfigureAwait(true);
                if (_disposed || cts.IsCancellationRequested || currentGeneration != _discoveryScanGeneration)
                {
                    return;
                }
                UpdateLastUpdated();
                QuotaStateUpdated?.Invoke();
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested || cancellationToken.IsCancellationRequested)
        {
            // Intentional cancellation
        }
        finally
        {
            if (currentGeneration == _discoveryScanGeneration)
            {
                _discoveryCts = null;
                IsDiscoveringProviders = false;
                UpdateVisibleProvidersCollection();
                if (isStartup)
                {
                    InitialStartupSettled?.Invoke();
                }
            }
        }
    }

    public Task RescanProvidersAsync(CancellationToken cancellationToken = default)
    {
        return DiscoverProvidersAsync(isStartup: false, cancellationToken);
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
            var eligible = Providers.Where(p => p.DiscoveryStatus != ProviderDiscoveryStatus.NotDetected).ToArray();
            if (eligible.Length > 0)
            {
                var refreshTasks = eligible.Select(p => p.RefreshAsync(cancellationToken)).ToArray();
                await Task.WhenAll(refreshTasks).ConfigureAwait(true);
            }
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

        _discoveryCts?.Cancel();
        _discoveryCts?.Dispose();
        _discoveryCts = null;

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
