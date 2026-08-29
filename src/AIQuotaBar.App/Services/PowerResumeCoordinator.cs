namespace AIQuotaBar.App.Services;

using System.Windows.Threading;
using Microsoft.Win32;

public interface IPowerResumeCoordinator : IDisposable
{
    void Start();
    void OnPowerModeChanged(PowerModes mode);
    bool IsPending { get; }
}

public sealed class PowerResumeCoordinator : IPowerResumeCoordinator
{
    private readonly Func<Task>? _asyncRefreshAction;
    private readonly Action? _syncRefreshAction;
    private readonly Dispatcher? _dispatcher;
    private readonly TimeSpan _resumeDelay;
    private readonly Func<Action, CancellationToken, Task>? _customDelayScheduler;

    private CancellationTokenSource? _pendingResumeCts;
    private bool _isStarted;
    private bool _disposed;

    public bool IsPending => _pendingResumeCts != null && !_pendingResumeCts.IsCancellationRequested;

    public PowerResumeCoordinator(
        Func<Task> refreshAction,
        Dispatcher? dispatcher = null,
        TimeSpan? resumeDelay = null,
        Func<Action, CancellationToken, Task>? customDelayScheduler = null)
    {
        _asyncRefreshAction = refreshAction ?? throw new ArgumentNullException(nameof(refreshAction));
        _dispatcher = dispatcher;
        _resumeDelay = resumeDelay ?? TimeSpan.FromSeconds(8);
        _customDelayScheduler = customDelayScheduler;
    }

    public PowerResumeCoordinator(
        Action refreshAction,
        Dispatcher? dispatcher = null,
        TimeSpan? resumeDelay = null,
        Func<Action, CancellationToken, Task>? customDelayScheduler = null)
    {
        _syncRefreshAction = refreshAction ?? throw new ArgumentNullException(nameof(refreshAction));
        _dispatcher = dispatcher;
        _resumeDelay = resumeDelay ?? TimeSpan.FromSeconds(8);
        _customDelayScheduler = customDelayScheduler;
    }

    public void Start()
    {
        if (_disposed || _isStarted)
        {
            return;
        }

        _isStarted = true;
        try
        {
            SystemEvents.PowerModeChanged += HandleSystemPowerModeChanged;
        }
        catch
        {
            // SystemEvents may fail if running without a Windows message pump in test environments
        }
    }

    private void HandleSystemPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        OnPowerModeChanged(e.Mode);
    }

    public void OnPowerModeChanged(PowerModes mode)
    {
        if (_disposed || mode != PowerModes.Resume)
        {
            return;
        }

        ScheduleResumeRecovery();
    }

    private void ScheduleResumeRecovery()
    {
        if (_disposed)
        {
            return;
        }

        // Cancel and coalesce any previously scheduled recovery
        _pendingResumeCts?.Cancel();
        _pendingResumeCts?.Dispose();

        var cts = new CancellationTokenSource();
        _pendingResumeCts = cts;

        _ = ExecuteDelayedRecoveryAsync(cts);
    }

    private async Task ExecuteDelayedRecoveryAsync(CancellationTokenSource cts)
    {
        try
        {
            if (_customDelayScheduler != null)
            {
                await _customDelayScheduler(() => { }, cts.Token).ConfigureAwait(false);
            }
            else
            {
                await Task.Delay(_resumeDelay, cts.Token).ConfigureAwait(false);
            }

            if (_disposed || cts.IsCancellationRequested)
            {
                return;
            }

            void Execute()
            {
                if (_disposed || cts.IsCancellationRequested)
                {
                    return;
                }

                if (_asyncRefreshAction != null)
                {
                    _ = _asyncRefreshAction();
                }
                else
                {
                    _syncRefreshAction?.Invoke();
                }
            }

            if (_dispatcher != null && !_dispatcher.CheckAccess())
            {
                _dispatcher.Invoke(Execute);
            }
            else
            {
                Execute();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation or coalescing
        }
        catch
        {
            // Transient errors during recovery must not crash the application
        }
        finally
        {
            if (ReferenceEquals(_pendingResumeCts, cts))
            {
                _pendingResumeCts = null;
            }
            cts.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_isStarted)
        {
            try
            {
                SystemEvents.PowerModeChanged -= HandleSystemPowerModeChanged;
            }
            catch
            {
                // Ignore any unhook errors on shutdown
            }
            _isStarted = false;
        }

        _pendingResumeCts?.Cancel();
        _pendingResumeCts?.Dispose();
        _pendingResumeCts = null;
    }
}
