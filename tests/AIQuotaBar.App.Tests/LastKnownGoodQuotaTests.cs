namespace AIQuotaBar.App.Tests;

using AIQuotaBar.App.Providers;
using AIQuotaBar.App.Tray;
using AIQuotaBar.App.ViewModels;
using AIQuotaBar.Core.Interfaces;
using AIQuotaBar.Core.Models;
using Xunit;

public class LastKnownGoodQuotaTests
{
    private sealed class StubUsageProvider : IUsageProvider
    {
        public string Id { get; init; } = "test-provider";
        public string DisplayName { get; init; } = "Test Provider";
        public Func<CancellationToken, Task<ProviderSnapshot>>? OnGetUsage { get; set; }

        public Task<ProviderSnapshot> GetUsageAsync(CancellationToken cancellationToken = default)
        {
            if (OnGetUsage != null)
            {
                return OnGetUsage(cancellationToken);
            }

            return Task.FromResult(new ProviderSnapshot(
                Id,
                DisplayName,
                ProviderStatus.Available,
                null,
                "Pro",
                windows: new[]
                {
                    new QuotaWindow("5h", "5-Hour", 40.0, TimeSpan.FromHours(5), DateTimeOffset.UtcNow.AddHours(3))
                }));
        }
    }

    [Fact]
    public void ScenarioA_SuccessThenTimeout_PreservesQuotaAndSetsStale()
    {
        var stub = new StubUsageProvider();
        var vm = new ProviderSectionViewModel(stub, TimeSpan.FromMinutes(1));

        var initialTime = DateTimeOffset.UtcNow.AddMinutes(-5);
        var successSnapshot = new ProviderSnapshot(
            stub.Id,
            stub.DisplayName,
            ProviderStatus.Available,
            null,
            "Pro",
            timestamp: initialTime,
            windows: new[]
            {
                new QuotaWindow("5h", "5-Hour", 40.0, TimeSpan.FromHours(5), initialTime.AddHours(3))
            });

        vm.ApplySnapshot(successSnapshot);

        Assert.Equal(ProviderStatus.Available, vm.Status);
        Assert.False(vm.IsQuotaStale);
        Assert.Equal(initialTime, vm.LastSuccessfulRefreshAt);
        Assert.Single(vm.VisibleWindows);
        Assert.Equal(60.0, vm.VisibleWindows[0].RemainingPercent);

        var timeoutTime = DateTimeOffset.UtcNow;
        var timeoutSnapshot = new ProviderSnapshot(
            stub.Id,
            stub.DisplayName,
            ProviderStatus.Timeout,
            "Provider timed out",
            "Pro",
            timestamp: timeoutTime,
            windows: Array.Empty<QuotaWindow>());

        vm.ApplySnapshot(timeoutSnapshot);

        // Windows are preserved!
        Assert.Equal(ProviderStatus.Timeout, vm.Status);
        Assert.True(vm.IsQuotaStale);
        Assert.True(vm.ShowStaleIndicator);
        Assert.False(vm.ShowStatusCard);
        Assert.Equal("Refresh timed out · showing last update", vm.StaleStatusText);
        Assert.Equal(initialTime, vm.LastSuccessfulRefreshAt); // LastSuccessfulRefreshAt unchanged!
        Assert.Equal(timeoutTime, vm.LastRefreshedAt);
        Assert.Single(vm.VisibleWindows);
        Assert.Equal(60.0, vm.VisibleWindows[0].RemainingPercent);
    }

    [Fact]
    public void ScenarioB_SuccessThenError_PreservesQuotaAndSetsStale()
    {
        var stub = new StubUsageProvider();
        var vm = new ProviderSectionViewModel(stub, TimeSpan.FromMinutes(1));

        var initialTime = DateTimeOffset.UtcNow.AddMinutes(-2);
        var successSnapshot = new ProviderSnapshot(
            stub.Id,
            stub.DisplayName,
            ProviderStatus.Available,
            null,
            "ChatGPT Plus",
            timestamp: initialTime,
            windows: new[]
            {
                new QuotaWindow("weekly", "Weekly", 20.0, TimeSpan.FromDays(7), initialTime.AddDays(4))
            });

        vm.ApplySnapshot(successSnapshot);

        var errorSnapshot = new ProviderSnapshot(
            stub.Id,
            stub.DisplayName,
            ProviderStatus.Error,
            "Communication failed",
            "ChatGPT Plus",
            timestamp: DateTimeOffset.UtcNow,
            windows: Array.Empty<QuotaWindow>());

        vm.ApplySnapshot(errorSnapshot);

        Assert.Equal(ProviderStatus.Error, vm.Status);
        Assert.True(vm.IsQuotaStale);
        Assert.True(vm.ShowStaleIndicator);
        Assert.Single(vm.VisibleWindows);
        Assert.Equal(80.0, vm.VisibleWindows[0].RemainingPercent);
        Assert.Equal(initialTime, vm.LastSuccessfulRefreshAt);
    }

    [Fact]
    public void ScenarioC_NoPreviousSuccess_Timeout_DoesNotManufactureWindows()
    {
        var stub = new StubUsageProvider();
        var vm = new ProviderSectionViewModel(stub, TimeSpan.FromMinutes(1));

        var timeoutSnapshot = new ProviderSnapshot(
            stub.Id,
            stub.DisplayName,
            ProviderStatus.Timeout,
            "Timeout on first poll",
            null,
            windows: Array.Empty<QuotaWindow>());

        vm.ApplySnapshot(timeoutSnapshot);

        Assert.Equal(ProviderStatus.Timeout, vm.Status);
        Assert.False(vm.IsQuotaStale);
        Assert.False(vm.ShowStaleIndicator);
        Assert.True(vm.ShowStatusCard);
        Assert.Empty(vm.Windows);
        Assert.Empty(vm.VisibleWindows);
        Assert.Null(vm.LastSuccessfulRefreshAt);
    }

    [Fact]
    public void ScenarioD_SuccessThenUnauthenticated_ClearsWindowsAndResetsStale()
    {
        var stub = new StubUsageProvider();
        var vm = new ProviderSectionViewModel(stub, TimeSpan.FromMinutes(1));

        var successSnapshot = new ProviderSnapshot(
            stub.Id,
            stub.DisplayName,
            ProviderStatus.Available,
            null,
            "Claude Pro",
            windows: new[]
            {
                new QuotaWindow("5h", "5-Hour", 10.0, TimeSpan.FromHours(5), DateTimeOffset.UtcNow.AddHours(4))
            });

        vm.ApplySnapshot(successSnapshot);
        Assert.Single(vm.VisibleWindows);

        var unauthSnapshot = new ProviderSnapshot(
            stub.Id,
            stub.DisplayName,
            ProviderStatus.Unauthenticated,
            "Claude Code requires sign-in",
            null,
            windows: Array.Empty<QuotaWindow>());

        vm.ApplySnapshot(unauthSnapshot);

        Assert.Equal(ProviderStatus.Unauthenticated, vm.Status);
        Assert.False(vm.IsQuotaStale);
        Assert.False(vm.ShowStaleIndicator);
        Assert.True(vm.ShowStatusCard);
        Assert.Equal("Claude Code requires sign-in", vm.StatusMessage);
        Assert.Empty(vm.Windows);
        Assert.Empty(vm.VisibleWindows);
        Assert.Null(vm.LastSuccessfulRefreshAt);
    }

    [Fact]
    public void ScenarioE_SuccessThenNotDetected_ClearsWindows()
    {
        var stub = new StubUsageProvider();
        var vm = new ProviderSectionViewModel(stub, TimeSpan.FromMinutes(1));

        var successSnapshot = new ProviderSnapshot(
            stub.Id,
            stub.DisplayName,
            ProviderStatus.Available,
            null,
            "Standard",
            windows: new[]
            {
                new QuotaWindow("weekly", "Weekly", 50.0, TimeSpan.FromDays(7), DateTimeOffset.UtcNow.AddDays(2))
            });

        vm.ApplySnapshot(successSnapshot);
        Assert.Single(vm.VisibleWindows);

        vm.ApplyDiscoveryStatus(ProviderDiscoveryStatus.NotDetected);

        Assert.Equal(ProviderDiscoveryStatus.NotDetected, vm.DiscoveryStatus);
        Assert.False(vm.IsQuotaStale);
        Assert.Empty(vm.Windows);
        Assert.Empty(vm.VisibleWindows);
        Assert.Null(vm.LastSuccessfulRefreshAt);
    }

    [Fact]
    public void ScenarioF_SuccessThenSemanticUnavailable_ClearsWindows()
    {
        var stub = new StubUsageProvider();
        var vm = new ProviderSectionViewModel(stub, TimeSpan.FromMinutes(1));

        var successSnapshot = new ProviderSnapshot(
            stub.Id,
            stub.DisplayName,
            ProviderStatus.Available,
            null,
            "Copilot Individual",
            windows: new[]
            {
                new QuotaWindow("premium", "Premium", 30.0, TimeSpan.FromDays(14), DateTimeOffset.UtcNow.AddDays(7))
            });

        vm.ApplySnapshot(successSnapshot);
        Assert.Single(vm.VisibleWindows);

        var endedSnapshot = new ProviderSnapshot(
            stub.Id,
            stub.DisplayName,
            ProviderStatus.Unavailable,
            "Copilot subscription has ended",
            "Copilot Individual",
            windows: Array.Empty<QuotaWindow>());

        vm.ApplySnapshot(endedSnapshot);

        Assert.Equal(ProviderStatus.Unavailable, vm.Status);
        Assert.False(vm.IsQuotaStale);
        Assert.Empty(vm.Windows);
        Assert.Empty(vm.VisibleWindows);
        Assert.Null(vm.LastSuccessfulRefreshAt);
        Assert.True(vm.ShowStatusCard);
    }

    [Fact]
    public void ScenarioG_SuccessThenTimeoutThenSuccess_ReplacesWithFreshData()
    {
        var stub = new StubUsageProvider();
        var vm = new ProviderSectionViewModel(stub, TimeSpan.FromMinutes(1));

        var time1 = DateTimeOffset.UtcNow.AddMinutes(-10);
        var snap1 = new ProviderSnapshot(
            stub.Id,
            stub.DisplayName,
            ProviderStatus.Available,
            null,
            "Free",
            timestamp: time1,
            windows: new[]
            {
                new QuotaWindow("weekly", "Weekly", 40.0, TimeSpan.FromDays(7), time1.AddDays(3))
            });

        vm.ApplySnapshot(snap1);

        var snapTimeout = new ProviderSnapshot(
            stub.Id,
            stub.DisplayName,
            ProviderStatus.Timeout,
            "Timeout",
            "Free",
            timestamp: time1.AddMinutes(5),
            windows: Array.Empty<QuotaWindow>());

        vm.ApplySnapshot(snapTimeout);
        Assert.True(vm.IsQuotaStale);
        Assert.Equal(60.0, vm.VisibleWindows[0].RemainingPercent);

        var time2 = DateTimeOffset.UtcNow;
        var snap2 = new ProviderSnapshot(
            stub.Id,
            stub.DisplayName,
            ProviderStatus.Available,
            null,
            "Free",
            timestamp: time2,
            windows: new[]
            {
                new QuotaWindow("weekly", "Weekly", 15.0, TimeSpan.FromDays(7), time2.AddDays(3))
            });

        vm.ApplySnapshot(snap2);

        Assert.Equal(ProviderStatus.Available, vm.Status);
        Assert.False(vm.IsQuotaStale);
        Assert.False(vm.ShowStaleIndicator);
        Assert.Equal(time2, vm.LastSuccessfulRefreshAt);
        Assert.Single(vm.VisibleWindows);
        Assert.Equal(85.0, vm.VisibleWindows[0].RemainingPercent);
    }

    [Fact]
    public void ScenarioH_And_I_StaleObservations_DoNotTriggerNotifications_UntilFreshSuccess()
    {
        var evaluator = new QuotaNotificationEvaluator();

        // 1. Initial observation: 40% remaining -> records baseline silently
        var initialObs = new[]
        {
            new QuotaObservation("test", "Test Provider", "5h", "5-Hour", 40.0, QuotaWindowStatus.Active, IsStale: false)
        };
        var notif1 = evaluator.Evaluate(initialObs);
        Assert.Null(notif1);

        // 2. PC sleeps, wake up, transient timeout -> observation is marked IsStale: true (still 40%)
        var staleObs = new[]
        {
            new QuotaObservation("test", "Test Provider", "5h", "5-Hour", 40.0, QuotaWindowStatus.Active, IsStale: true)
        };
        var notifStale = evaluator.Evaluate(staleObs);
        Assert.Null(notifStale); // NO notification fired while stale!

        // 3. Next real successful poll returns 8% remaining (below 10% threshold)
        var recoveryObs = new[]
        {
            new QuotaObservation("test", "Test Provider", "5h", "5-Hour", 8.0, QuotaWindowStatus.Active, IsStale: false)
        };
        var notifRecovery = evaluator.Evaluate(recoveryObs);

        Assert.NotNull(notifRecovery);
        Assert.Equal(QuotaNotificationType.LowQuota, notifRecovery.Type);
        Assert.Contains("8%", notifRecovery.Message);
    }

    [Fact]
    public void ScenarioJ_SuccessThenCancelledRefresh_PreservesQuotaWithoutStaleFlag()
    {
        var stub = new StubUsageProvider();
        var vm = new ProviderSectionViewModel(stub, TimeSpan.FromMinutes(1));

        var initialTime = DateTimeOffset.UtcNow.AddMinutes(-5);
        var successSnapshot = new ProviderSnapshot(
            stub.Id,
            stub.DisplayName,
            ProviderStatus.Available,
            null,
            "Pro",
            timestamp: initialTime,
            windows: new[]
            {
                new QuotaWindow("5h", "5-Hour", 40.0, TimeSpan.FromHours(5), initialTime.AddHours(3))
            });

        vm.ApplySnapshot(successSnapshot);

        Assert.Equal(ProviderStatus.Available, vm.Status);
        Assert.False(vm.IsQuotaStale);
        Assert.Equal(initialTime, vm.LastSuccessfulRefreshAt);
        Assert.Single(vm.VisibleWindows);
        Assert.Equal(60.0, vm.VisibleWindows[0].RemainingPercent);

        var cancelledSnapshot = new ProviderSnapshot(
            stub.Id,
            stub.DisplayName,
            ProviderStatus.Cancelled,
            null,
            "Pro",
            timestamp: DateTimeOffset.UtcNow,
            windows: Array.Empty<QuotaWindow>());

        vm.ApplySnapshot(cancelledSnapshot);

        // Quota is preserved, and IsQuotaStale is FALSE because cancellation is control-flow, not a failure!
        Assert.Equal(ProviderStatus.Available, vm.Status);
        Assert.False(vm.IsQuotaStale);
        Assert.False(vm.ShowStaleIndicator);
        Assert.Equal(initialTime, vm.LastSuccessfulRefreshAt);
        Assert.Single(vm.VisibleWindows);
        Assert.Equal(60.0, vm.VisibleWindows[0].RemainingPercent);
    }

    [Fact]
    public void ScenarioK_CancelledRefreshBeforeAnySuccess_DoesNotFabricateState()
    {
        var stub = new StubUsageProvider();
        var vm = new ProviderSectionViewModel(stub, TimeSpan.FromMinutes(1));

        var cancelledSnapshot = new ProviderSnapshot(
            stub.Id,
            stub.DisplayName,
            ProviderStatus.Cancelled,
            null,
            null,
            windows: Array.Empty<QuotaWindow>());

        vm.ApplySnapshot(cancelledSnapshot);

        Assert.Equal(ProviderStatus.Cancelled, vm.Status);
        Assert.False(vm.IsQuotaStale);
        Assert.Empty(vm.Windows);
        Assert.Empty(vm.VisibleWindows);
        Assert.Null(vm.LastSuccessfulRefreshAt);
    }

    [Fact]
    public void ScenarioL_SuccessThenCancellationThenSuccess_ReplacesWithFreshData()
    {
        var stub = new StubUsageProvider();
        var vm = new ProviderSectionViewModel(stub, TimeSpan.FromMinutes(1));

        var time1 = DateTimeOffset.UtcNow.AddMinutes(-10);
        var snap1 = new ProviderSnapshot(
            stub.Id,
            stub.DisplayName,
            ProviderStatus.Available,
            null,
            "Pro",
            timestamp: time1,
            windows: new[]
            {
                new QuotaWindow("5h", "5-Hour", 40.0, TimeSpan.FromHours(5), time1.AddHours(3))
            });

        vm.ApplySnapshot(snap1);

        var snapCancelled = new ProviderSnapshot(
            stub.Id,
            stub.DisplayName,
            ProviderStatus.Cancelled,
            null,
            "Pro",
            timestamp: time1.AddMinutes(2),
            windows: Array.Empty<QuotaWindow>());

        vm.ApplySnapshot(snapCancelled);
        Assert.Equal(60.0, vm.VisibleWindows[0].RemainingPercent);

        var time2 = DateTimeOffset.UtcNow;
        var snap2 = new ProviderSnapshot(
            stub.Id,
            stub.DisplayName,
            ProviderStatus.Available,
            null,
            "Pro",
            timestamp: time2,
            windows: new[]
            {
                new QuotaWindow("5h", "5-Hour", 60.0, TimeSpan.FromHours(5), time2.AddHours(3))
            });

        vm.ApplySnapshot(snap2);

        Assert.Equal(ProviderStatus.Available, vm.Status);
        Assert.False(vm.IsQuotaStale);
        Assert.Equal(time2, vm.LastSuccessfulRefreshAt);
        Assert.Single(vm.VisibleWindows);
        Assert.Equal(40.0, vm.VisibleWindows[0].RemainingPercent);
    }

    [Fact]
    public void ScenarioM_And_N_CancelledRefresh_PreservesTrayHealth_AndDoesNotTriggerNotifications()
    {
        var stub = new StubUsageProvider();
        var vm = new ProviderSectionViewModel(stub, TimeSpan.FromMinutes(1));

        var snap1 = new ProviderSnapshot(
            stub.Id,
            stub.DisplayName,
            ProviderStatus.Available,
            null,
            "Pro",
            windows: new[]
            {
                new QuotaWindow("5h", "5-Hour", 40.0, TimeSpan.FromHours(5), DateTimeOffset.UtcNow.AddHours(3))
            });

        vm.ApplySnapshot(snap1);

        // Calculate tray health with real quota
        var tray1 = TrayHealthCalculator.Calculate(new[] { vm });
        Assert.True(tray1.HasVisibleQuotaData);
        Assert.Equal(60.0, tray1.LowestRemainingPercent);

        // Cancelled refresh occurs
        var snapCancelled = new ProviderSnapshot(
            stub.Id,
            stub.DisplayName,
            ProviderStatus.Cancelled,
            null,
            "Pro",
            windows: Array.Empty<QuotaWindow>());

        vm.ApplySnapshot(snapCancelled);

        // Tray continues using previous real quota!
        var tray2 = TrayHealthCalculator.Calculate(new[] { vm });
        Assert.True(tray2.HasVisibleQuotaData);
        Assert.Equal(60.0, tray2.LowestRemainingPercent);

        // Notifications evaluator gets observation with preserved quota
        var evaluator = new QuotaNotificationEvaluator();
        var obs = new[]
        {
            new QuotaObservation(vm.ProviderId, vm.ProviderName, "5h", "5-Hour", 60.0, QuotaWindowStatus.Active, IsStale: vm.IsQuotaStale)
        };
        var notif = evaluator.Evaluate(obs);
        Assert.Null(notif); // Baseline recorded silently, no false triggers
    }
}
