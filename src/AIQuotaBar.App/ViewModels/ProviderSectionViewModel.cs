namespace AIQuotaBar.App.ViewModels;

using System.Collections.ObjectModel;
using AIQuotaBar.App.Layout;
using AIQuotaBar.Core.Interfaces;
using AIQuotaBar.Core.Models;

public sealed class ProviderSectionViewModel : ViewModelBase, IDisposable
{
    private readonly IUsageProvider _provider;
    private readonly TimeSpan _refreshInterval;
    private CancellationTokenSource? _currentRefreshCts;
    private bool _disposed;
    private WidgetLayoutMode _layoutMode = WidgetLayoutMode.Full;

    private bool _isCompactMode;
    private bool _isLoading;
    private string _providerName;
    private string? _accountPlan;
    private ProviderStatus _status = ProviderStatus.Available;
    private string? _statusMessage;
    private DateTimeOffset? _lastRefreshedAt;

    public string ProviderId => _provider.Id;
    public IUsageProvider Provider => _provider;
    public TimeSpan RefreshInterval => _refreshInterval;

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
                foreach (var window in Windows)
                {
                    window.LayoutMode = value;
                }
            }
        }
    }

    public string ProviderAccentColor => ProviderId.ToLowerInvariant() switch
    {
        "codex" => "#10B981",       // Emerald green
        "antigravity" => "#38BDF8", // Cyan / Sky blue
        _ => "#10B981"
    };

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
            }
        }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public DateTimeOffset? LastRefreshedAt
    {
        get => _lastRefreshedAt;
        private set => SetProperty(ref _lastRefreshedAt, value);
    }

    public ObservableCollection<QuotaWindowViewModel> Windows { get; } = new();
    public bool HasWindows => Windows.Count > 0;

    public ProviderSectionViewModel(IUsageProvider provider, TimeSpan refreshInterval)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _refreshInterval = refreshInterval;
        _providerName = provider.DisplayName;
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || IsLoading)
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

            if (!_disposed && !cts.IsCancellationRequested && ReferenceEquals(_currentRefreshCts, cts))
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
            if (!_disposed && !cts.IsCancellationRequested && ReferenceEquals(_currentRefreshCts, cts))
            {
                Status = ProviderStatus.Error;
                StatusMessage = $"Unable to communicate with {_provider.DisplayName}";
            }
        }
        finally
        {
            if (ReferenceEquals(_currentRefreshCts, cts))
            {
                IsLoading = false;
            }
        }
    }

    public void RefreshCountdown()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var window in Windows)
        {
            window.RefreshCountdown();
        }
    }

    public void ApplySnapshot(ProviderSnapshot snapshot)
    {
        ProviderName = snapshot.ProviderDisplayName;
        AccountPlan = snapshot.AccountPlan;
        Status = snapshot.Status;
        StatusMessage = snapshot.StatusMessage;
        LastRefreshedAt = snapshot.Timestamp;

        Windows.Clear();
        foreach (var window in snapshot.Windows)
        {
            var windowVm = new QuotaWindowViewModel(window, ProviderId)
            {
                LayoutMode = _layoutMode
            };
            Windows.Add(windowVm);
        }

        OnPropertyChanged(nameof(HasWindows));
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
