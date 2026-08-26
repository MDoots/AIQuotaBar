namespace AIQuotaBar.App.ViewModels;

using System.Collections.ObjectModel;
using System.Windows;
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

    public bool CanRefresh => !IsLoading;

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
        _autoRefreshTimer.Tick += async (s, e) => await RefreshAsync();

        // Local countdown display refresh every 30 seconds (no process spawn)
        _countdownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _countdownTimer.Tick += (s, e) => UpdateCountdowns();
    }

    public void Start()
    {
        _autoRefreshTimer.Start();
        _countdownTimer.Start();
        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        _currentRefreshCts?.Cancel();
        _currentRefreshCts?.Dispose();
        _currentRefreshCts = new CancellationTokenSource();

        try
        {
            var snapshot = await Task.Run(
                () => _provider.GetUsageAsync(_currentRefreshCts.Token),
                _currentRefreshCts.Token);

            Application.Current?.Dispatcher.Invoke(() => ApplySnapshot(snapshot));
        }
        catch (OperationCanceledException)
        {
            // Ignore manual or cleanup cancellation
        }
        catch (Exception ex)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                Status = ProviderStatus.Error;
                StatusMessage = $"Unexpected error: {ex.Message}";
            });
        }
        finally
        {
            IsLoading = false;
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

    private void UpdateCountdowns()
    {
        // Re-notify countdown properties on existing windows to refresh "resets in Xm"
        foreach (var window in Windows)
        {
            window.RefreshCountdown();
        }
    }

    public void Dispose()
    {
        _autoRefreshTimer.Stop();
        _countdownTimer.Stop();
        _currentRefreshCts?.Cancel();
        _currentRefreshCts?.Dispose();
    }
}
