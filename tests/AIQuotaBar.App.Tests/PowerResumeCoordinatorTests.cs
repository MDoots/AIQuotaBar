namespace AIQuotaBar.App.Tests;

using AIQuotaBar.App.Services;
using Microsoft.Win32;
using Xunit;

public class PowerResumeCoordinatorTests
{
    [Fact]
    public async Task OnPowerModeChanged_Resume_ExecutesRecoveryAfterDelay()
    {
        var refreshTcs = new TaskCompletionSource<bool>();
        var coordinator = new PowerResumeCoordinator(
            refreshAction: () =>
            {
                refreshTcs.TrySetResult(true);
                return Task.CompletedTask;
            },
            resumeDelay: TimeSpan.FromMilliseconds(30));

        coordinator.Start();
        Assert.False(coordinator.IsPending);

        coordinator.OnPowerModeChanged(PowerModes.Resume);
        Assert.True(coordinator.IsPending);

        // Wait for execution deterministically
        var completed = await Task.WhenAny(refreshTcs.Task, Task.Delay(2000));
        Assert.Same(refreshTcs.Task, completed);

        // Give finally block a micro-yield to clear pending reference
        for (var i = 0; i < 50 && coordinator.IsPending; i++)
        {
            await Task.Delay(10);
        }

        Assert.False(coordinator.IsPending);

        coordinator.Dispose();
    }

    [Fact]
    public async Task OnPowerModeChanged_MultipleResumeEvents_CoalesceIntoSingleRefresh()
    {
        var refreshCount = 0;
        var refreshTcs = new TaskCompletionSource<bool>();
        var coordinator = new PowerResumeCoordinator(
            refreshAction: () =>
            {
                Interlocked.Increment(ref refreshCount);
                refreshTcs.TrySetResult(true);
                return Task.CompletedTask;
            },
            resumeDelay: TimeSpan.FromMilliseconds(50));

        coordinator.Start();

        // Fire 5 rapid Resume events within the grace window
        coordinator.OnPowerModeChanged(PowerModes.Resume);
        await Task.Delay(10);
        coordinator.OnPowerModeChanged(PowerModes.Resume);
        await Task.Delay(10);
        coordinator.OnPowerModeChanged(PowerModes.Resume);
        await Task.Delay(10);
        coordinator.OnPowerModeChanged(PowerModes.Resume);

        Assert.True(coordinator.IsPending);

        // Wait for coalesced execution
        var completed = await Task.WhenAny(refreshTcs.Task, Task.Delay(2000));
        Assert.Same(refreshTcs.Task, completed);

        // Allow any trailing timer to settle
        await Task.Delay(100);

        Assert.Equal(1, refreshCount); // Coalesced into exactly 1 refresh!

        coordinator.Dispose();
    }

    [Fact]
    public async Task OnPowerModeChanged_NonResumeEvents_AreIgnored()
    {
        var refreshCount = 0;
        var coordinator = new PowerResumeCoordinator(
            refreshAction: () =>
            {
                Interlocked.Increment(ref refreshCount);
                return Task.CompletedTask;
            },
            resumeDelay: TimeSpan.FromMilliseconds(30));

        coordinator.Start();

        coordinator.OnPowerModeChanged(PowerModes.StatusChange);
        coordinator.OnPowerModeChanged(PowerModes.Suspend);

        Assert.False(coordinator.IsPending);

        await Task.Delay(60);

        Assert.Equal(0, refreshCount);

        coordinator.Dispose();
    }

    [Fact]
    public async Task Dispose_CancelsPendingRecoveryWithoutException()
    {
        var refreshCount = 0;
        var coordinator = new PowerResumeCoordinator(
            refreshAction: () =>
            {
                Interlocked.Increment(ref refreshCount);
                return Task.CompletedTask;
            },
            resumeDelay: TimeSpan.FromMilliseconds(150));

        coordinator.Start();
        coordinator.OnPowerModeChanged(PowerModes.Resume);
        Assert.True(coordinator.IsPending);

        coordinator.Dispose();

        await Task.Delay(200);

        Assert.Equal(0, refreshCount);
        Assert.False(coordinator.IsPending);
    }

    [Fact]
    public async Task RecoveryActionException_DoesNotCrashCoordinator()
    {
        var coordinator = new PowerResumeCoordinator(
            refreshAction: () => throw new InvalidOperationException("Simulated provider refresh transient network error"),
            resumeDelay: TimeSpan.FromMilliseconds(20));

        coordinator.Start();
        coordinator.OnPowerModeChanged(PowerModes.Resume);

        for (var i = 0; i < 50 && coordinator.IsPending; i++)
        {
            await Task.Delay(10);
        }

        Assert.False(coordinator.IsPending);

        coordinator.Dispose();
    }
}
