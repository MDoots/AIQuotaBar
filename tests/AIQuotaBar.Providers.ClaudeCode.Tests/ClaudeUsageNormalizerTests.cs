namespace AIQuotaBar.Providers.ClaudeCode.Tests;

using AIQuotaBar.Core.Models;
using AIQuotaBar.Providers.ClaudeCode.Normalization;
using Xunit;

public class ClaudeUsageNormalizerTests
{
    [Fact]
    public void Normalize_WithNullOrEmpty_ReturnsUnavailable()
    {
        var snapshot = ClaudeUsageNormalizer.Normalize(null);

        Assert.Equal("claude-code", snapshot.ProviderId);
        Assert.Equal("Claude Code", snapshot.ProviderDisplayName);
        Assert.Equal(ProviderStatus.Unavailable, snapshot.Status);
        Assert.Equal("No usage data returned by Claude Code", snapshot.StatusMessage);
    }

    [Fact]
    public void Normalize_WithUnauthenticatedMessage_ReturnsUnauthenticated()
    {
        var raw = "Error: Authentication required. Please run `claude login` to authenticate.";
        var snapshot = ClaudeUsageNormalizer.Normalize(raw);

        Assert.Equal(ProviderStatus.Unauthenticated, snapshot.Status);
        Assert.Equal("Claude Code requires sign-in", snapshot.StatusMessage);
    }

    [Fact]
    public void Normalize_WithAnsiAndMultipleWindows_NormalizesCorrectly()
    {
        var raw = "\u001b[1mUsage Limits:\u001b[0m\r\n" +
                  "\u001b[32mCurrent session allowance: 15% used (resets in 3h 45m)\u001b[0m\r\n" +
                  "\u001b[34mWeekly limit: 42% used\u001b[0m\r\n" +
                  "\u001b[35mOpus weekly limit: 80% used\u001b[0m\r\n";

        var snapshot = ClaudeUsageNormalizer.Normalize(raw, "Claude Pro");

        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Equal("Claude Pro", snapshot.AccountPlan);
        Assert.Equal(3, snapshot.Windows.Count);

        var sessionWindow = snapshot.Windows[0];
        Assert.Equal("session-5h", sessionWindow.Id);
        Assert.Equal("5-Hour Session", sessionWindow.DisplayName);
        Assert.Equal(15.0, sessionWindow.RawUsedPercent);
        Assert.Equal(85.0, sessionWindow.RemainingPercent);
        Assert.Equal(TimeSpan.FromHours(5), sessionWindow.Duration);
        Assert.NotNull(sessionWindow.ResetsAt);

        var weeklyWindow = snapshot.Windows[1];
        Assert.Equal("weekly-all", weeklyWindow.Id);
        Assert.Equal("Weekly", weeklyWindow.DisplayName);
        Assert.Equal(42.0, weeklyWindow.RawUsedPercent);
        Assert.Equal(58.0, weeklyWindow.RemainingPercent);
        Assert.Equal(TimeSpan.FromDays(7), weeklyWindow.Duration);

        var opusWindow = snapshot.Windows[2];
        Assert.Equal("weekly-opus", opusWindow.Id);
        Assert.Equal("Opus · Weekly", opusWindow.DisplayName);
        Assert.Equal(80.0, opusWindow.RawUsedPercent);
        Assert.Equal(20.0, opusWindow.RemainingPercent);
    }

    [Fact]
    public void Normalize_WithExhaustedAllowance_SetsExhaustedStatus()
    {
        var raw = "Current session limit: 100% used";
        var snapshot = ClaudeUsageNormalizer.Normalize(raw);

        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Single(snapshot.Windows);
        Assert.Equal(100.0, snapshot.Windows[0].RawUsedPercent);
        Assert.Equal(0.0, snapshot.Windows[0].RemainingPercent);
        Assert.Equal(QuotaWindowStatus.Exhausted, snapshot.Windows[0].Status);
    }

    [Fact]
    public void Normalize_WithApiKeyOrUsageBasedAuth_ReturnsTruthfulNoFixedQuota()
    {
        var raw = "Authenticated via API Key (pay-as-you-go). No subscription quota limits exist.";
        var snapshot = ClaudeUsageNormalizer.Normalize(raw);

        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Equal("Usage-based billing — no fixed Claude Code quota", snapshot.StatusMessage);
        Assert.Empty(snapshot.Windows);
    }

    [Fact]
    public void Normalize_WithPartialSections_ExtractsAvailableWindows()
    {
        var raw = "Weekly allowance: 25% consumed (resets in 2 days 4 hours)";
        var snapshot = ClaudeUsageNormalizer.Normalize(raw);

        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Single(snapshot.Windows);
        Assert.Equal("weekly-all", snapshot.Windows[0].Id);
        Assert.Equal("Weekly", snapshot.Windows[0].DisplayName);
        Assert.Equal(75.0, snapshot.Windows[0].RemainingPercent);
        Assert.NotNull(snapshot.Windows[0].ResetsAt);
    }
}
