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
        var baseTime = new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);
        var raw = "\u001b[1mUsage Limits:\u001b[0m\r\n" +
                  "\u001b[32mCurrent session allowance: 15% used (resets in 3h 45m)\u001b[0m\r\n" +
                  "\u001b[34mWeekly limit: 42% used\u001b[0m\r\n" +
                  "\u001b[35mOpus weekly limit: 80% used\u001b[0m\r\n";

        var snapshot = ClaudeUsageNormalizer.Normalize(raw, "Claude Pro", baseTime);

        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Equal("Claude Pro", snapshot.AccountPlan);
        Assert.Equal(3, snapshot.Windows.Count);

        var sessionWindow = snapshot.Windows[0];
        Assert.Equal("session-5h", sessionWindow.Id);
        Assert.Equal("Session · 5-hour", sessionWindow.DisplayName);
        Assert.Equal(15.0, sessionWindow.RawUsedPercent);
        Assert.Equal(85.0, sessionWindow.RemainingPercent);
        Assert.Equal(TimeSpan.FromHours(5), sessionWindow.Duration);
        Assert.Equal(baseTime.AddHours(3).AddMinutes(45), sessionWindow.ResetsAt);

        var weeklyWindow = snapshot.Windows[1];
        Assert.Equal("weekly-all", weeklyWindow.Id);
        Assert.Equal("Weekly · All models", weeklyWindow.DisplayName);
        Assert.Equal(42.0, weeklyWindow.RawUsedPercent);
        Assert.Equal(58.0, weeklyWindow.RemainingPercent);
        Assert.Equal(TimeSpan.FromDays(7), weeklyWindow.Duration);

        var opusWindow = snapshot.Windows[2];
        Assert.Equal("weekly-opus", opusWindow.Id);
        Assert.Equal("Weekly · Claude Opus", opusWindow.DisplayName);
        Assert.Equal(80.0, opusWindow.RawUsedPercent);
        Assert.Equal(20.0, opusWindow.RemainingPercent);
    }

    [Fact]
    public void Normalize_WithExplicitRemainingPercentage_ConvertsToUsedPercent()
    {
        var raw = "Current session allowance: 58% remaining";
        var snapshot = ClaudeUsageNormalizer.Normalize(raw);

        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Single(snapshot.Windows);
        Assert.Equal(42.0, snapshot.Windows[0].RawUsedPercent);
        Assert.Equal(58.0, snapshot.Windows[0].RemainingPercent);
    }

    [Fact]
    public void Normalize_WithBareAmbiguousPercent_IsIgnoredSafely()
    {
        // Bare "42%" without used/consumed/remaining/left must be ignored
        var raw = "Some miscellaneous telemetry: 42% accuracy";
        var snapshot = ClaudeUsageNormalizer.Normalize(raw);

        Assert.Equal(ProviderStatus.Unavailable, snapshot.Status);
        Assert.Empty(snapshot.Windows);
    }

    [Fact]
    public void Normalize_WithUnknownSection_IgnoresUnknownAndDoesNotInventWeeklySonnet()
    {
        var raw = "Sonnet weekly limit: 50% used\r\n" +
                  "Current session: 20% used";
        var snapshot = ClaudeUsageNormalizer.Normalize(raw);

        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Single(snapshot.Windows); // Only session-5h is understood, sonnet is NOT fabricated
        Assert.Equal("session-5h", snapshot.Windows[0].Id);
    }

    [Fact]
    public void Normalize_WithTimeOnlyReset_ResolvesToNextFutureOccurrence()
    {
        var baseTime = new DateTimeOffset(2026, 8, 29, 20, 0, 0, TimeSpan.Zero);
        // "resets at 01:00" is earlier in the day than 20:00 -> should advance to tomorrow 01:00
        var raw = "Current session allowance: 10% used (resets at 01:00)";
        var snapshot = ClaudeUsageNormalizer.Normalize(raw, now: baseTime);

        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Single(snapshot.Windows);
        Assert.NotNull(snapshot.Windows[0].ResetsAt);
        Assert.Equal(new DateTimeOffset(2026, 8, 30, 1, 0, 0, TimeSpan.Zero), snapshot.Windows[0].ResetsAt);
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
}
