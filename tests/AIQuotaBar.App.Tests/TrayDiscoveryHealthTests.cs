namespace AIQuotaBar.App.Tests;

using AIQuotaBar.App.Health;
using AIQuotaBar.App.Providers;
using AIQuotaBar.App.Tray;
using AIQuotaBar.App.ViewModels;
using AIQuotaBar.Core.Interfaces;
using AIQuotaBar.Core.Models;
using Xunit;

public class TrayDiscoveryHealthTests
{
    private sealed class StubUsageProvider : IUsageProvider
    {
        public string Id { get; }
        public string DisplayName { get; }

        public StubUsageProvider(string id, string displayName)
        {
            Id = id;
            DisplayName = displayName;
        }

        public Task<ProviderSnapshot> GetUsageAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProviderSnapshot(Id, DisplayName, ProviderStatus.Available));
    }

    [Fact]
    public void TrayHealthCalculator_ZeroDetectedProviders_ReturnsNoProvidersDetected()
    {
        var section1 = new ProviderSectionViewModel(new StubUsageProvider("codex", "OpenAI Codex"), TimeSpan.FromSeconds(60), "Codex", ProviderDiscoveryStatus.NotDetected);
        var section2 = new ProviderSectionViewModel(new StubUsageProvider("antigravity", "Google Antigravity"), TimeSpan.FromSeconds(180), "Antigravity", ProviderDiscoveryStatus.NotDetected);

        var state = TrayHealthCalculator.Calculate(new[] { section1, section2 });

        Assert.Equal("AIQuotaBar — No providers detected", state.TooltipText);
        Assert.Equal("No supported providers detected", state.StatusMenuText);
        Assert.Equal(QuotaHealthLevel.Neutral, state.HealthLevel);
        Assert.False(state.HasVisibleQuotaData);
        Assert.False(state.HasVisibleProviders);
    }

    [Fact]
    public void TrayHealthCalculator_DetectedProviders_WaitingForData_ReturnsWaitingForData()
    {
        var section = new ProviderSectionViewModel(new StubUsageProvider("codex", "OpenAI Codex"), TimeSpan.FromSeconds(60), "Codex", ProviderDiscoveryStatus.Detected);

        var state = TrayHealthCalculator.Calculate(new[] { section });

        Assert.Equal("AIQuotaBar — Waiting for quota data", state.TooltipText);
        Assert.Equal("Waiting for quota data", state.StatusMenuText);
        Assert.Equal(QuotaHealthLevel.Neutral, state.HealthLevel);
        Assert.False(state.HasVisibleQuotaData);
        Assert.True(state.HasVisibleProviders);
    }

    [Fact]
    public void TrayHealthCalculator_DetectedProviders_UserDeselectedAll_ReturnsNoQuotaRowsSelected()
    {
        var section = new ProviderSectionViewModel(new StubUsageProvider("codex", "OpenAI Codex"), TimeSpan.FromSeconds(60), "Codex", ProviderDiscoveryStatus.Detected);
        section.ApplySnapshot(new ProviderSnapshot(
            "codex", "OpenAI Codex", ProviderStatus.Available,
            windows: new[] { new QuotaWindow("primary", "5-Hour", 30.0, TimeSpan.FromHours(5), null, QuotaWindowStatus.Active) }));

        // Hide provider by setting IsVisibleByPreference to false
        section.IsVisibleByPreference = false;

        var state = TrayHealthCalculator.Calculate(new[] { section });

        Assert.Equal("AIQuotaBar — No quota rows selected", state.TooltipText);
        Assert.Equal("No quota rows selected", state.StatusMenuText);
        Assert.Equal(QuotaHealthLevel.Neutral, state.HealthLevel);
    }

    [Fact]
    public void QuotaNotificationEvaluator_NewlyDiscoveredLowQuota_BaselinesSilently()
    {
        var evaluator = new QuotaNotificationEvaluator();

        // First snapshot with 5% remaining (low quota)
        var obs = new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "primary", "5-Hour", 5.0, QuotaWindowStatus.Active)
        };

        var notification = evaluator.Evaluate(obs, notificationsEnabled: true);

        // Per Slice 3 invariants: first observation baselines silently, no alert
        Assert.Null(notification);

        // Second snapshot remains at 5%: no crossing, no alert
        var notification2 = evaluator.Evaluate(obs, notificationsEnabled: true);
        Assert.Null(notification2);

        // Third snapshot drops to 0% (exhausted): generates exhausted alert!
        var obsExhausted = new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "primary", "5-Hour", 0.0, QuotaWindowStatus.Exhausted)
        };

        var notification3 = evaluator.Evaluate(obsExhausted, notificationsEnabled: true);
        Assert.NotNull(notification3);
        Assert.Contains("exhausted", notification3.Title, StringComparison.OrdinalIgnoreCase);
    }
}
