namespace AIQuotaBar.App.ViewModels;

using System.Collections.ObjectModel;
using System.Windows;
using AIQuotaBar.App.Layout;
using AIQuotaBar.App.Providers;
using AIQuotaBar.App.Settings;
using AIQuotaBar.Core.Interfaces;
using AIQuotaBar.Core.Models;

public sealed class ProviderSectionViewModel : ViewModelBase, IDisposable
{
    private readonly IUsageProvider _provider;
    private readonly TimeSpan _refreshInterval;
    private readonly string? _shortDisplayName;
    private readonly string _accentColor;
    private CancellationTokenSource? _currentRefreshCts;
    private bool _disposed;
    private WidgetLayoutMode _layoutMode = WidgetLayoutMode.Full;

    private ProviderDiscoveryStatus _discoveryStatus = ProviderDiscoveryStatus.Unknown;
    private bool _isCompactMode;
    private bool _isVisibleByPreference = true;
    private bool _isLoading;
    private string _providerName;
    private string? _accountPlan;
    private ProviderStatus _status = ProviderStatus.Available;
    private string? _statusMessage;
    private DateTimeOffset? _lastRefreshedAt;
    private DateTimeOffset? _lastSuccessfulRefreshAt;
    private bool _isQuotaStale;
    private AppSettings? _lastSettings;
    private readonly List<QuotaWindowViewModel> _allWindows = new();

    public string ProviderId => _provider.Id;
    public IUsageProvider Provider => _provider;
    public TimeSpan RefreshInterval => _refreshInterval;

    public event Action? SnapshotApplied;

    public ProviderDiscoveryStatus DiscoveryStatus
    {
        get => _discoveryStatus;
        set
        {
            if (SetProperty(ref _discoveryStatus, value))
            {
                OnPropertyChanged(nameof(IsProviderDetected));
                OnPropertyChanged(nameof(IsProviderNotDetected));
                OnPropertyChanged(nameof(ShouldDisplayInWidget));
            }
        }
    }

    public bool IsProviderDetected => DiscoveryStatus == ProviderDiscoveryStatus.Detected;
    public bool IsProviderNotDetected => DiscoveryStatus == ProviderDiscoveryStatus.NotDetected;

    public bool IsVisibleByPreference
    {
        get => _isVisibleByPreference;
        set
        {
            if (SetProperty(ref _isVisibleByPreference, value))
            {
                OnPropertyChanged(nameof(ShouldDisplayInWidget));
            }
        }
    }

    public bool ShouldDisplayInWidget =>
        IsVisibleByPreference &&
        DiscoveryStatus != ProviderDiscoveryStatus.NotDetected &&
        DiscoveryStatus != ProviderDiscoveryStatus.Checking &&
        (VisibleWindows.Count > 0 || (_allWindows.Count == 0 && (HasStatusMessage || IsLoading)));

    public bool IsCompactMode
    {
        get => _isCompactMode;
        set
        {
            if (SetProperty(ref _isCompactMode, value))
            {
                OnPropertyChanged(nameof(ShowAccountPlan));
            }
        }
    }

    public WidgetLayoutMode LayoutMode
    {
        get => _layoutMode;
        set
        {
            if (_layoutMode != value)
            {
                _layoutMode = value;
                OnPropertyChanged(nameof(LayoutMode));
                OnPropertyChanged(nameof(DisplayProviderName));
                OnPropertyChanged(nameof(ShowAccountPlan));
                OnPropertyChanged(nameof(CardPadding));
                foreach (var window in _allWindows)
                {
                    window.LayoutMode = value;
                }
            }
        }
    }

    public Thickness CardPadding => _layoutMode == WidgetLayoutMode.Micro ? new Thickness(5, 5, 5, 5) : new Thickness(8, 6, 8, 6);

    public string ProviderAccentColor => _accentColor;

    public string ProviderName
    {
        get => _providerName;
        private set
        {
            if (SetProperty(ref _providerName, value))
            {
                OnPropertyChanged(nameof(DisplayProviderName));
            }
        }
    }

    public string DisplayProviderName => ProviderLabelFormatter.Format(ProviderName, _layoutMode);

    public string ShortDisplayName => _shortDisplayName ?? ProviderName;

    public string DockedDisplayName => ShortDisplayName;

    public string? AccountPlan
    {
        get => _accountPlan;
        private set
        {
            if (SetProperty(ref _accountPlan, value))
            {
                OnPropertyChanged(nameof(HasAccountPlan));
                OnPropertyChanged(nameof(ShowAccountPlan));
            }
        }
    }

    public bool HasAccountPlan => !string.IsNullOrWhiteSpace(AccountPlan);
    public bool ShowAccountPlan => _layoutMode == WidgetLayoutMode.Full && !_isCompactMode && HasAccountPlan;

    public ProviderStatus Status
    {
        get => _status;
        private set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(IsAvailable));
                OnPropertyChanged(nameof(HasError));
                OnPropertyChanged(nameof(StaleStatusText));
            }
        }
    }

    public bool IsAvailable => Status == ProviderStatus.Available;
    public bool HasError => Status != ProviderStatus.Available;

    public string? StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatusMessage));
                OnPropertyChanged(nameof(ShowStatusCard));
                OnPropertyChanged(nameof(ShouldDisplayInWidget));
            }
        }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool IsQuotaStale
    {
        get => _isQuotaStale;
        private set
        {
            if (SetProperty(ref _isQuotaStale, value))
            {
                OnPropertyChanged(nameof(ShowStaleIndicator));
                OnPropertyChanged(nameof(ShowStatusCard));
                OnPropertyChanged(nameof(StaleStatusText));
            }
        }
    }

    public bool ShowStaleIndicator => IsQuotaStale && HasVisibleWindows;
    public bool ShowStatusCard => HasStatusMessage && !HasVisibleWindows;
    public string StaleStatusText => Status == ProviderStatus.Timeout
        ? "Refresh timed out · showing last update"
        : "Refresh failed · showing last update";

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(ShouldDisplayInWidget));
            }
        }
    }

    public DateTimeOffset? LastRefreshedAt
    {
        get => _lastRefreshedAt;
        private set => SetProperty(ref _lastRefreshedAt, value);
    }

    public DateTimeOffset? LastSuccessfulRefreshAt
    {
        get => _lastSuccessfulRefreshAt;
        private set => SetProperty(ref _lastSuccessfulRefreshAt, value);
    }

    public IReadOnlyList<QuotaWindowViewModel> AllWindows => _allWindows;
    public ObservableCollection<QuotaWindowViewModel> Windows { get; } = new();
    public ObservableCollection<QuotaWindowViewModel> VisibleWindows { get; } = new();
    public bool HasWindows => Windows.Count > 0;
    public bool HasVisibleWindows => VisibleWindows.Count > 0;

    public ProviderSectionViewModel(
        IUsageProvider provider,
        TimeSpan refreshInterval,
        string? shortDisplayName = null,
        string? accentColor = null,
        ProviderDiscoveryStatus initialDiscoveryStatus = ProviderDiscoveryStatus.Detected)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _refreshInterval = refreshInterval;
        _providerName = provider.DisplayName;

        _shortDisplayName = shortDisplayName ?? provider.DisplayName;
        _accentColor = !string.IsNullOrWhiteSpace(accentColor)
            ? accentColor
            : "#6B7280";
        _discoveryStatus = initialDiscoveryStatus;
    }

    public ProviderSectionViewModel(
        IUsageProvider provider,
        TimeSpan refreshInterval,
        string? shortDisplayName,
        ProviderDiscoveryStatus initialDiscoveryStatus)
        : this(provider, refreshInterval, shortDisplayName, null, initialDiscoveryStatus)
    {
    }

    public void ApplyDiscoveryStatus(ProviderDiscoveryStatus status)
    {
        var wasDetectedOrHasWindows = _discoveryStatus == ProviderDiscoveryStatus.Detected || _allWindows.Count > 0;
        DiscoveryStatus = status;

        if (status == ProviderDiscoveryStatus.NotDetected)
        {
            _currentRefreshCts?.Cancel();
            _currentRefreshCts?.Dispose();
            _currentRefreshCts = null;

            if (wasDetectedOrHasWindows)
            {
                void Clear()
                {
                    _allWindows.Clear();
                    Windows.Clear();
                    VisibleWindows.Clear();
                    AccountPlan = null;
                    Status = ProviderStatus.Unavailable;
                    StatusMessage = null;
                    LastRefreshedAt = null;
                    LastSuccessfulRefreshAt = null;
                    IsQuotaStale = false;

                    OnPropertyChanged(nameof(HasWindows));
                    OnPropertyChanged(nameof(HasVisibleWindows));
                    OnPropertyChanged(nameof(HasAccountPlan));
                    OnPropertyChanged(nameof(HasStatusMessage));
                    OnPropertyChanged(nameof(ShowStatusCard));
                    OnPropertyChanged(nameof(ShowStaleIndicator));
                    OnPropertyChanged(nameof(ShouldDisplayInWidget));
                }

                if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
                {
                    Application.Current.Dispatcher.Invoke(Clear);
                }
                else
                {
                    Clear();
                }
            }
        }
        else if (status == ProviderDiscoveryStatus.Error)
        {
            Status = ProviderStatus.Unavailable;
            StatusMessage = "Local provider check encountered an error.";
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || IsLoading || DiscoveryStatus == ProviderDiscoveryStatus.NotDetected)
        {
            return;
        }

        IsLoading = true;
        _currentRefreshCts?.Cancel();
        _currentRefreshCts?.Dispose();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _currentRefreshCts = cts;

        try
        {
            var snapshot = await _provider.GetUsageAsync(cts.Token).ConfigureAwait(true);

            if (!_disposed && !cts.IsCancellationRequested && ReferenceEquals(_currentRefreshCts, cts) && DiscoveryStatus != ProviderDiscoveryStatus.NotDetected)
            {
                ApplySnapshot(snapshot);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || cts.IsCancellationRequested)
        {
            // Intentional cancellation
        }
        catch (Exception)
        {
            if (!_disposed && !cts.IsCancellationRequested && ReferenceEquals(_currentRefreshCts, cts) && DiscoveryStatus != ProviderDiscoveryStatus.NotDetected)
            {
                LastRefreshedAt = DateTimeOffset.UtcNow;
                if (_allWindows.Count > 0)
                {
                    Status = ProviderStatus.Error;
                    IsQuotaStale = true;
                    StatusMessage = "Refresh failed · showing last update";
                    ApplyVisibilityFilterInternal(_lastSettings);
                }
                else
                {
                    Status = ProviderStatus.Error;
                    IsQuotaStale = false;
                    StatusMessage = $"Unable to communicate with {_provider.DisplayName}";
                }
            }
        }
        finally
        {
            if (ReferenceEquals(_currentRefreshCts, cts))
            {
                _currentRefreshCts = null;
                IsLoading = false;
                OnPropertyChanged(nameof(ShouldDisplayInWidget));
            }
        }
    }

    public void RefreshCountdown()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var window in _allWindows)
        {
            window.RefreshCountdown();
        }
    }

    public void ApplySnapshot(ProviderSnapshot snapshot)
    {
        if (_disposed || DiscoveryStatus == ProviderDiscoveryStatus.NotDetected)
        {
            return;
        }

        void Apply()
        {
            if (_disposed || DiscoveryStatus == ProviderDiscoveryStatus.NotDetected)
            {
                return;
            }

            if (_discoveryStatus is ProviderDiscoveryStatus.Checking or ProviderDiscoveryStatus.Unknown)
            {
                DiscoveryStatus = ProviderDiscoveryStatus.Detected;
            }

            if (snapshot.Status == ProviderStatus.Available && snapshot.Windows.Count > 0)
            {
                ProviderName = snapshot.ProviderDisplayName;
                AccountPlan = snapshot.AccountPlan;
                Status = ProviderStatus.Available;
                StatusMessage = snapshot.StatusMessage;
                LastRefreshedAt = snapshot.Timestamp;
                LastSuccessfulRefreshAt = snapshot.Timestamp;
                IsQuotaStale = false;

                _allWindows.Clear();
                Windows.Clear();
                foreach (var window in snapshot.Windows)
                {
                    var windowVm = new QuotaWindowViewModel(window, ProviderId)
                    {
                        LayoutMode = _layoutMode
                    };
                    _allWindows.Add(windowVm);
                    Windows.Add(windowVm);
                }

                ApplyVisibilityFilterInternal(_lastSettings);
                OnPropertyChanged(nameof(HasWindows));
            }
            else if (snapshot.Status == ProviderStatus.Available && snapshot.Windows.Count == 0)
            {
                // Available without finite quota windows (e.g. usage-based API key billing)
                ProviderName = snapshot.ProviderDisplayName;
                AccountPlan = snapshot.AccountPlan;
                Status = ProviderStatus.Available;
                StatusMessage = snapshot.StatusMessage;
                LastRefreshedAt = snapshot.Timestamp;
                // Do not update LastSuccessfulRefreshAt because no finite quota observation occurred
                IsQuotaStale = false;

                _allWindows.Clear();
                Windows.Clear();
                VisibleWindows.Clear();

                OnPropertyChanged(nameof(HasWindows));
                OnPropertyChanged(nameof(HasVisibleWindows));
                OnPropertyChanged(nameof(ShouldDisplayInWidget));
            }
            else if (snapshot.Status is ProviderStatus.Timeout or ProviderStatus.Error)
            {
                LastRefreshedAt = snapshot.Timestamp;

                if (_allWindows.Count > 0)
                {
                    // Transient technical failure: preserve last-known-good quota windows
                    Status = snapshot.Status;
                    IsQuotaStale = true;
                    StatusMessage = snapshot.Status == ProviderStatus.Timeout
                        ? "Refresh timed out · showing last update"
                        : "Refresh failed · showing last update";

                    ApplyVisibilityFilterInternal(_lastSettings);
                }
                else
                {
                    // Transient failure with no prior success
                    _allWindows.Clear();
                    Windows.Clear();
                    VisibleWindows.Clear();
                    IsQuotaStale = false;
                    Status = snapshot.Status;
                    StatusMessage = snapshot.StatusMessage ?? (snapshot.Status == ProviderStatus.Timeout
                        ? "Provider did not respond"
                        : $"Unable to communicate with {ProviderName}");

                    OnPropertyChanged(nameof(HasWindows));
                    OnPropertyChanged(nameof(HasVisibleWindows));
                    OnPropertyChanged(nameof(ShouldDisplayInWidget));
                }
            }
            else if (snapshot.Status == ProviderStatus.Cancelled)
            {
                // Cancellation is control-flow / transport state, NOT a semantic entitlement change.
                if (_allWindows.Count > 0)
                {
                    // Preserve existing last-known-good quota windows, AccountPlan, and freshness state
                    return;
                }
                else
                {
                    _allWindows.Clear();
                    Windows.Clear();
                    VisibleWindows.Clear();
                    IsQuotaStale = false;
                    Status = ProviderStatus.Cancelled;
                    StatusMessage = null;

                    OnPropertyChanged(nameof(HasWindows));
                    OnPropertyChanged(nameof(HasVisibleWindows));
                    OnPropertyChanged(nameof(ShouldDisplayInWidget));
                }
            }
            else
            {
                // Semantic state change (Unauthenticated, Unavailable without finite quota)
                _allWindows.Clear();
                Windows.Clear();
                VisibleWindows.Clear();
                IsQuotaStale = false;
                LastSuccessfulRefreshAt = null;

                ProviderName = snapshot.ProviderDisplayName;
                AccountPlan = snapshot.AccountPlan;
                Status = snapshot.Status;
                StatusMessage = snapshot.StatusMessage;
                LastRefreshedAt = snapshot.Timestamp;

                OnPropertyChanged(nameof(HasWindows));
                OnPropertyChanged(nameof(HasVisibleWindows));
                OnPropertyChanged(nameof(ShouldDisplayInWidget));
            }
        }

        if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.Invoke(Apply);
        }
        else
        {
            Apply();
        }

        if (DiscoveryStatus != ProviderDiscoveryStatus.NotDetected)
        {
            SnapshotApplied?.Invoke();
        }
    }

    public void ApplyVisibilityFilter(AppSettings? settings)
    {
        _lastSettings = settings;
        ApplyVisibilityFilterInternal(settings);
    }

    private void ApplyVisibilityFilterInternal(AppSettings? settings)
    {
        void Filter()
        {
            if (settings != null)
            {
                IsVisibleByPreference = settings.IsProviderVisible(ProviderId);
            }

            VisibleWindows.Clear();
            foreach (var window in _allWindows)
            {
                var isVisible = settings == null || settings.IsQuotaWindowVisible(ProviderId, window.Id);
                if (isVisible)
                {
                    VisibleWindows.Add(window);
                }
            }

            OnPropertyChanged(nameof(HasVisibleWindows));
            OnPropertyChanged(nameof(ShouldDisplayInWidget));
        }

        if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.Invoke(Filter);
        }
        else
        {
            Filter();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        _currentRefreshCts?.Cancel();
        _currentRefreshCts?.Dispose();
        _currentRefreshCts = null;

        IsLoading = false;
    }
}
