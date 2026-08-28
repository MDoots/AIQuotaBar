namespace AIQuotaBar.App.Tests;

using AIQuotaBar.App.Settings;
using AIQuotaBar.App.Tray;
using AIQuotaBar.App.ViewModels;
using AIQuotaBar.Core.Interfaces;
using AIQuotaBar.Core.Models;
using Xunit;

public class TrayEventingTests
{
    private sealed class MockUsageProvider : IUsageProvider
    {
        private readonly Func<CancellationToken, Task<ProviderSnapshot>> _handler;

        public string Id { get; }
        public string DisplayName { get; }

        public MockUsageProvider(
            string id,
            string displayName,
            Func<CancellationToken, Task<ProviderSnapshot>> handler)
        {
            Id = id;
            DisplayName = displayName;
            _handler = handler;
        }

        public Task<ProviderSnapshot> GetUsageAsync(CancellationToken cancellationToken = default)
        {
            return _handler(cancellationToken);
        }
    }

    [Fact]
    public async Task RefreshAllAsync_RaisesQuotaStateUpdated_ExactlyOnceForBatch()
    {
        var snapshot1 = new ProviderSnapshot("codex", "OpenAI Codex", ProviderStatus.Available, windows: new[]
        {
            new QuotaWindow("5h", "5-Hour", 20, null, null)
        });
        var snapshot2 = new ProviderSnapshot("antigravity", "Google Antigravity", ProviderStatus.Available, windows: new[]
        {
            new QuotaWindow("weekly", "Gemini · Weekly", 25, null, null)
        });

        var provider1 = new MockUsageProvider("codex", "OpenAI Codex", _ => Task.FromResult(snapshot1));
        var provider2 = new MockUsageProvider("antigravity", "Google Antigravity", _ => Task.FromResult(snapshot2));

        var section1 = new ProviderSectionViewModel(provider1, TimeSpan.FromMinutes(1));
        var section2 = new ProviderSectionViewModel(provider2, TimeSpan.FromMinutes(1));

        using var vm = new WidgetViewModel(new[] { section1, section2 });

        var quotaUpdatedCount = 0;
        vm.QuotaStateUpdated += () => quotaUpdatedCount++;

        await vm.RefreshAllAsync();

        // Exactly one event for the entire multi-provider batch, not one per provider!
        Assert.Equal(1, quotaUpdatedCount);
    }

    [Fact]
    public async Task RefreshAllAsync_TwoProvidersCrossBelow10_EvaluatesOnceAndAggregates()
    {
        // 1. Initial baseline: both providers above 10%
        var baseline1 = new ProviderSnapshot("codex", "OpenAI Codex", ProviderStatus.Available, windows: new[]
        {
            new QuotaWindow("5h", "5-Hour", 85, null, null) // 15% remaining
        });
        var baseline2 = new ProviderSnapshot("antigravity", "Google Antigravity", ProviderStatus.Available, windows: new[]
        {
            new QuotaWindow("gemini_weekly", "Gemini · Weekly", 80, null, null) // 20% remaining
        });

        var currentSnap1 = baseline1;
        var currentSnap2 = baseline2;

        var provider1 = new MockUsageProvider("codex", "OpenAI Codex", _ => Task.FromResult(currentSnap1));
        var provider2 = new MockUsageProvider("antigravity", "Google Antigravity", _ => Task.FromResult(currentSnap2));

        var section1 = new ProviderSectionViewModel(provider1, TimeSpan.FromMinutes(1));
        var section2 = new ProviderSectionViewModel(provider2, TimeSpan.FromMinutes(1));

        section1.ApplySnapshot(baseline1);
        section2.ApplySnapshot(baseline2);

        using var vm = new WidgetViewModel(new[] { section1, section2 });
        var evaluator = new QuotaNotificationEvaluator();

        // Baseline evaluation
        var baselineObservations = vm.Providers
            .Where(p => p.IsVisibleByPreference)
            .SelectMany(p => p.VisibleWindows.Select(w => new QuotaObservation(p.ProviderId, p.ProviderName, w.Id, w.RawDisplayName, w.RemainingPercent, w.Status)))
            .ToList();
        evaluator.Evaluate(baselineObservations);

        // 2. Both providers drop below 10% on next refresh
        currentSnap1 = new ProviderSnapshot("codex", "OpenAI Codex", ProviderStatus.Available, windows: new[]
        {
            new QuotaWindow("5h", "5-Hour", 93, null, null) // 7% remaining
        });
        currentSnap2 = new ProviderSnapshot("antigravity", "Google Antigravity", ProviderStatus.Available, windows: new[]
        {
            new QuotaWindow("gemini_weekly", "Gemini · Weekly", 92, null, null) // 8% remaining
        });

        var notificationsReceived = new List<QuotaNotification>();
        vm.QuotaStateUpdated += () =>
        {
            var obs = vm.Providers
                .Where(p => p.IsVisibleByPreference)
                .SelectMany(p => p.VisibleWindows.Select(w => new QuotaObservation(p.ProviderId, p.ProviderName, w.Id, w.RawDisplayName, w.RemainingPercent, w.Status)))
                .ToList();

            var notification = evaluator.Evaluate(obs, notificationsEnabled: true);
            if (notification != null)
            {
                notificationsReceived.Add(notification);
            }
        };

        // User / app triggers manual refresh all
        await vm.RefreshAllAsync();

        // Must produce EXACTLY ONE aggregated notification for the entire multi-provider refresh!
        Assert.Single(notificationsReceived);
        var notif = notificationsReceived[0];
        Assert.Equal("AIQuotaBar — Low quota", notif.Title);
        Assert.Contains("Codex 5-Hour has 7% remaining", notif.Message);
        Assert.Contains("1 other quota window is also low", notif.Message);
    }

    [Fact]
    public async Task ProviderAutoRefresh_OneProviderCrosses_RaisesSingleEvaluation()
    {
        var baseline = new ProviderSnapshot("codex", "OpenAI Codex", ProviderStatus.Available, windows: new[]
        {
            new QuotaWindow("5h", "5-Hour", 80, null, null) // 20% remaining
        });

        var currentSnapshot = baseline;
        var provider = new MockUsageProvider("codex", "OpenAI Codex", _ => Task.FromResult(currentSnapshot));
        var section = new ProviderSectionViewModel(provider, TimeSpan.FromMinutes(1));
        section.ApplySnapshot(baseline);

        using var vm = new WidgetViewModel(new[] { section });
        var evaluator = new QuotaNotificationEvaluator();

        var baselineObs = vm.Providers
            .SelectMany(p => p.VisibleWindows.Select(w => new QuotaObservation(p.ProviderId, p.ProviderName, w.Id, w.RawDisplayName, w.RemainingPercent, w.Status)))
            .ToList();
        evaluator.Evaluate(baselineObs);

        var notifications = new List<QuotaNotification>();
        vm.QuotaStateUpdated += () =>
        {
            var obs = vm.Providers
                .SelectMany(p => p.VisibleWindows.Select(w => new QuotaObservation(p.ProviderId, p.ProviderName, w.Id, w.RawDisplayName, w.RemainingPercent, w.Status)))
                .ToList();
            var n = evaluator.Evaluate(obs, notificationsEnabled: true);
            if (n != null) notifications.Add(n);
        };

        // Provider timer refresh runs
        currentSnapshot = new ProviderSnapshot("codex", "OpenAI Codex", ProviderStatus.Available, windows: new[]
        {
            new QuotaWindow("5h", "5-Hour", 92, null, null) // 8% remaining
        });
        await section.RefreshAsync();

        Assert.Single(notifications);
        Assert.Equal("AIQuotaBar — Low quota", notifications[0].Title);
    }

    [Fact]
    public async Task RefreshAllAsync_OnlyOneProviderCrosses_EvaluatesOnceAndProducesSingleNotification()
    {
        var baseline1 = new ProviderSnapshot("codex", "OpenAI Codex", ProviderStatus.Available, windows: new[]
        {
            new QuotaWindow("5h", "5-Hour", 80, null, null) // 20% remaining
        });
        var baseline2 = new ProviderSnapshot("antigravity", "Google Antigravity", ProviderStatus.Available, windows: new[]
        {
            new QuotaWindow("gemini_weekly", "Gemini · Weekly", 50, null, null) // 50% remaining
        });

        var currentSnap1 = baseline1;
        var currentSnap2 = baseline2;

        var provider1 = new MockUsageProvider("codex", "OpenAI Codex", _ => Task.FromResult(currentSnap1));
        var provider2 = new MockUsageProvider("antigravity", "Google Antigravity", _ => Task.FromResult(currentSnap2));

        var section1 = new ProviderSectionViewModel(provider1, TimeSpan.FromMinutes(1));
        var section2 = new ProviderSectionViewModel(provider2, TimeSpan.FromMinutes(1));

        section1.ApplySnapshot(baseline1);
        section2.ApplySnapshot(baseline2);

        using var vm = new WidgetViewModel(new[] { section1, section2 });
        var evaluator = new QuotaNotificationEvaluator();

        var baselineObs = vm.Providers
            .SelectMany(p => p.VisibleWindows.Select(w => new QuotaObservation(p.ProviderId, p.ProviderName, w.Id, w.RawDisplayName, w.RemainingPercent, w.Status)))
            .ToList();
        evaluator.Evaluate(baselineObs);

        // Only Codex crosses
        currentSnap1 = new ProviderSnapshot("codex", "OpenAI Codex", ProviderStatus.Available, windows: new[]
        {
            new QuotaWindow("5h", "5-Hour", 93, null, null) // 7% remaining
        });

        var notifications = new List<QuotaNotification>();
        vm.QuotaStateUpdated += () =>
        {
            var obs = vm.Providers
                .SelectMany(p => p.VisibleWindows.Select(w => new QuotaObservation(p.ProviderId, p.ProviderName, w.Id, w.RawDisplayName, w.RemainingPercent, w.Status)))
                .ToList();
            var n = evaluator.Evaluate(obs, notificationsEnabled: true);
            if (n != null) notifications.Add(n);
        };

        await vm.RefreshAllAsync();

        Assert.Single(notifications);
        Assert.Equal("AIQuotaBar — Low quota", notifications[0].Title);
        Assert.DoesNotContain("other quota window", notifications[0].Message);
    }

    [Fact]
    public void ApplySnapshot_RaisesQuotaStateUpdated()
    {
        var snapshot = new ProviderSnapshot("codex", "OpenAI Codex", ProviderStatus.Available, windows: new[]
        {
            new QuotaWindow("5h", "5-Hour", 20, null, null)
        });

        var provider = new MockUsageProvider("codex", "OpenAI Codex", _ => Task.FromResult(snapshot));
        var section = new ProviderSectionViewModel(provider, TimeSpan.FromMinutes(1));

        using var vm = new WidgetViewModel(new[] { section });

        var quotaUpdatedCount = 0;
        vm.QuotaStateUpdated += () => quotaUpdatedCount++;

        section.ApplySnapshot(snapshot);

        Assert.Equal(1, quotaUpdatedCount);
    }

    [Fact]
    public void UpdateVisibility_RaisesVisibilityStateUpdated_WithoutQuotaStateUpdated()
    {
        var snapshot = new ProviderSnapshot("codex", "OpenAI Codex", ProviderStatus.Available, windows: new[]
        {
            new QuotaWindow("5h", "5-Hour", 20, null, null)
        });

        var provider = new MockUsageProvider("codex", "OpenAI Codex", _ => Task.FromResult(snapshot));
        var section = new ProviderSectionViewModel(provider, TimeSpan.FromMinutes(1));
        section.ApplySnapshot(snapshot);

        using var vm = new WidgetViewModel(new[] { section });

        var quotaUpdatedCount = 0;
        var visibilityUpdatedCount = 0;

        vm.QuotaStateUpdated += () => quotaUpdatedCount++;
        vm.VisibilityStateUpdated += () => visibilityUpdatedCount++;

        var settings = new AppSettings();
        settings.SetProviderVisible("codex", false);

        vm.UpdateVisibility(settings);

        Assert.Equal(1, visibilityUpdatedCount);
        Assert.Equal(0, quotaUpdatedCount);
    }

    [Fact]
    public void Dispose_UnsubscribesEvents()
    {
        var provider = new MockUsageProvider("codex", "OpenAI Codex", _ => Task.FromResult(new ProviderSnapshot("codex", "OpenAI Codex", ProviderStatus.Available)));
        var section = new ProviderSectionViewModel(provider, TimeSpan.FromMinutes(1));

        var vm = new WidgetViewModel(new[] { section });

        var quotaUpdatedCount = 0;
        vm.QuotaStateUpdated += () => quotaUpdatedCount++;

        vm.Dispose();

        section.ApplySnapshot(new ProviderSnapshot("codex", "OpenAI Codex", ProviderStatus.Available));

        Assert.Equal(0, quotaUpdatedCount);
    }
}
