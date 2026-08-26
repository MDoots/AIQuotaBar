namespace AIQuotaBar.App.ViewModels;

using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using AIQuotaBar.Core.Interfaces;
using AIQuotaBar.Core.Models;
using AIQuotaBar.Providers.Codex;

public sealed class WidgetViewModel : ViewModelBase, IDisposable
{
    private readonly IUsageProvider _provider;
    private readonly DispatcherTimer _autoRefreshTimer;
    private readonly DispatcherTimer _countdownTimer;
    private CancellationTokenSource? _currentRefreshCts;
    private bool _disposed;

    private bool _isLoading;
    private string _providerName = "OpenAI Codex";
    private string? _accountPlan;
    private ProviderStatus _status = ProviderStatus.Available;
    private string? _statusMessage;
    private string _lastUpdatedText = "Not updated yet";

    public ObservableCollection<QuotaWindowViewModel> Windows { get; } = new();

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(CanRefresh));
            }
        }
    }

    public string ProviderName
    {
        get => _providerName;
        private set => SetProperty(ref _providerName, value);
    }

    public string? AccountPlan
    {
        get => _accountPlan;
        private set
        {
            if (SetProperty(ref _accountPlan, value))
            {
                OnPropertyChanged(nameof(HasAccountPlan));
            }
        }
    }

    public bool HasAccountPlan => !string.IsNullOrWhiteSpace(AccountPlan);

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
    public bool HasWindows => Windows.Count > 0;

    public string LastUpdatedText
    {
        get => _lastUpdatedText;
        private set => SetProperty(ref _lastUpdatedText, value);
    }

    public bool CanRefresh => !IsLoading && !_disposed;

    public ICommand RefreshCommand { get; }

    public WidgetViewModel(IUsageProvider? provider = null)
    {
        _provider = provider ?? new CodexUsageProvider();
        ProviderName = _provider.DisplayName;

        RefreshCommand = new RelayCommand(async () => await RefreshAsync(), () => CanRefresh);

        // Auto refresh every 120 seconds
        _autoRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(120)
        };
        _autoRefreshTimer.Tick += OnAutoRefreshTimerTick;

        // Local countdown display refresh every 30 seconds (no process spawn)
        _countdownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _countdownTimer.Tick += OnCountdownTimerTick;
    }

    public void Start()
    {
        if (_disposed)
        {
            return;
        }

        _autoRefreshTimer.Start();
        _countdownTimer.Start();
        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        if (_disposed || IsLoading)
        {
            return;
        }

        IsLoading = true;
        _currentRefreshCts?.Cancel();
        _currentRefreshCts?.Dispose();
        var cts = new CancellationTokenSource();
        _currentRefreshCts = cts;

        try
        {
            var snapshot = await _provider.GetUsageAsync(cts.Token).ConfigureAwait(true);

            // Prevent applying stale data if refreshed again, cancelled, or disposed
            if (!_disposed && !cts.IsCancellationRequested && ReferenceEquals(_currentRefreshCts, cts))
            {
                ApplySnapshot(snapshot);
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore intentional cancellation
        }
        catch
        {
            if (!_disposed && !cts.IsCancellationRequested && ReferenceEquals(_currentRefreshCts, cts))
            {
                Status = ProviderStatus.Error;
                StatusMessage = "Unable to communicate with Codex";
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

    private void OnAutoRefreshTimerTick(object? sender, EventArgs e)
    {
        if (!_disposed && !_isLoading)
        {
            _ = RefreshAsync();
        }
    }

    private void OnCountdownTimerTick(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        // Re-notify countdown properties on existing windows to refresh "resets in Xm"
        foreach (var window in Windows)
        {
            window.RefreshCountdown();
        }
    }

    private void ApplySnapshot(ProviderSnapshot snapshot)
    {
        ProviderName = snapshot.ProviderDisplayName;
        AccountPlan = snapshot.AccountPlan;
        Status = snapshot.Status;
        StatusMessage = snapshot.StatusMessage;
        LastUpdatedText = $"Updated {snapshot.Timestamp.ToLocalTime():HH:mm:ss}";

        Windows.Clear();
        foreach (var window in snapshot.Windows)
        {
            Windows.Add(new QuotaWindowViewModel(window));
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

        _autoRefreshTimer.Stop();
        _autoRefreshTimer.Tick -= OnAutoRefreshTimerTick;

        _countdownTimer.Stop();
        _countdownTimer.Tick -= OnCountdownTimerTick;

        _currentRefreshCts?.Cancel();
        _currentRefreshCts?.Dispose();
        _currentRefreshCts = null;

        IsLoading = false;
    }
}
