namespace AIQuotaBar.Providers.Codex.Tests;

using System.Text.Json;
using AIQuotaBar.Core.Models;
using AIQuotaBar.Providers.Codex.Normalization;
using AIQuotaBar.Providers.Codex.Protocol;
using Xunit;

public class CodexUsageNormalizerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void Normalize_FullPlusResponse_ReturnsBothWindowsAndAvailableStatus()
    {
        var json = File.ReadAllText(Path.Combine("Fixtures", "codex_plus_full.json"));
        var rateLimits = JsonSerializer.Deserialize<CodexRateLimitsResult>(json, JsonOptions);

        var snapshot = CodexUsageNormalizer.Normalize(rateLimits);

        Assert.Equal("codex", snapshot.ProviderId);
        Assert.Equal("OpenAI Codex", snapshot.ProviderDisplayName);
        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Equal("ChatGPT Plus", snapshot.AccountPlan);
        Assert.Equal(2, snapshot.Windows.Count);

        var primary = snapshot.Windows[0];
        Assert.Equal("primary", primary.Id);
        Assert.Equal("5-Hour", primary.DisplayName);
        Assert.Equal(28, primary.RawUsedPercent);
        Assert.Equal(72, primary.RemainingPercent);
        Assert.Equal(TimeSpan.FromHours(5), primary.Duration);
        Assert.NotNull(primary.ResetsAt);
        Assert.Equal(QuotaWindowStatus.Active, primary.Status);

        var secondary = snapshot.Windows[1];
        Assert.Equal("secondary", secondary.Id);
        Assert.Equal("Weekly", secondary.DisplayName);
        Assert.Equal(46, secondary.RawUsedPercent);
        Assert.Equal(54, secondary.RemainingPercent);
        Assert.Equal(TimeSpan.FromDays(7), secondary.Duration);
        Assert.NotNull(secondary.ResetsAt);
        Assert.Equal(QuotaWindowStatus.Active, secondary.Status);
    }

    [Fact]
    public void Normalize_WeeklyOnlyResponse_DoesNotManufacturePrimaryWindow()
    {
        var json = File.ReadAllText(Path.Combine("Fixtures", "codex_weekly_only.json"));
        var rateLimits = JsonSerializer.Deserialize<CodexRateLimitsResult>(json, JsonOptions);

        var snapshot = CodexUsageNormalizer.Normalize(rateLimits);

        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Single(snapshot.Windows);

        var weekly = snapshot.Windows[0];
        Assert.Equal("secondary", weekly.Id);
        Assert.Equal("Weekly", weekly.DisplayName);
        Assert.Equal(65, weekly.RawUsedPercent);
        Assert.Equal(35, weekly.RemainingPercent);
    }

    [Fact]
    public void Normalize_UnknownDurationResponse_FormatsDynamicLabel()
    {
        var json = File.ReadAllText(Path.Combine("Fixtures", "codex_unknown_duration.json"));
        var rateLimits = JsonSerializer.Deserialize<CodexRateLimitsResult>(json, JsonOptions);

        var snapshot = CodexUsageNormalizer.Normalize(rateLimits);

        Assert.Equal(2, snapshot.Windows.Count);
        Assert.Equal("45-Minute", snapshot.Windows[0].DisplayName);
        Assert.Equal("3-Day", snapshot.Windows[1].DisplayName);
    }

    [Fact]
    public void Normalize_MissingResets_PreservesNullResetsAt()
    {
        var json = File.ReadAllText(Path.Combine("Fixtures", "codex_missing_resets.json"));
        var rateLimits = JsonSerializer.Deserialize<CodexRateLimitsResult>(json, JsonOptions);

        var snapshot = CodexUsageNormalizer.Normalize(rateLimits);

        Assert.Single(snapshot.Windows);
        Assert.Null(snapshot.Windows[0].ResetsAt);
    }

    [Fact]
    public void Normalize_UnauthenticatedAccount_ReturnsUnauthenticatedStatus()
    {
        var accountResult = new CodexAccountResult
        {
            Account = null,
            RequiresOpenaiAuth = true
        };

        var snapshot = CodexUsageNormalizer.Normalize(null, accountResult);

        Assert.Equal(ProviderStatus.Unauthenticated, snapshot.Status);
        Assert.Empty(snapshot.Windows);
    }

    [Fact]
    public void Normalize_ExhaustedWindow_SetsStatusToExhausted()
    {
        var rateLimits = new CodexRateLimitsResult
        {
            RateLimits = new CodexRateLimitSnapshot
            {
                Primary = new CodexRateLimitWindow
                {
                    UsedPercent = 100,
                    WindowDurationMins = 300
                }
            }
        };

        var snapshot = CodexUsageNormalizer.Normalize(rateLimits);
        Assert.Single(snapshot.Windows);
        Assert.Equal(QuotaWindowStatus.Exhausted, snapshot.Windows[0].Status);
        Assert.Equal(0, snapshot.Windows[0].RemainingPercent);
    }

    [Fact]
    public void Normalize_FreeAccount_WithFiniteRateLimits_ReturnsAvailable()
    {
        var rateLimits = new CodexRateLimitsResult
        {
            RateLimits = new CodexRateLimitSnapshot
            {
                PlanType = "free",
                Primary = new CodexRateLimitWindow
                {
                    UsedPercent = 20,
                    WindowDurationMins = 300
                }
            }
        };

        var snapshot = CodexUsageNormalizer.Normalize(rateLimits);
        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Equal("ChatGPT Free", snapshot.AccountPlan);
        Assert.Single(snapshot.Windows);
        Assert.Equal(80.0, snapshot.Windows[0].RemainingPercent);
    }

    [Fact]
    public void Normalize_NullPlan_WithFiniteRateLimits_ReturnsAvailable()
    {
        var rateLimits = new CodexRateLimitsResult
        {
            RateLimits = new CodexRateLimitSnapshot
            {
                PlanType = null,
                Primary = new CodexRateLimitWindow
                {
                    UsedPercent = 45,
                    WindowDurationMins = 300
                }
            }
        };

        var snapshot = CodexUsageNormalizer.Normalize(rateLimits);
        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Null(snapshot.AccountPlan);
        Assert.Single(snapshot.Windows);
        Assert.Equal(55.0, snapshot.Windows[0].RemainingPercent);
    }

    [Fact]
    public void Normalize_NullRateLimits_ReturnsUnavailable()
    {
        var snapshot = CodexUsageNormalizer.Normalize(null, null);
        Assert.Equal(ProviderStatus.Unavailable, snapshot.Status);
        Assert.Equal("No rate limit data returned", snapshot.StatusMessage);
        Assert.Empty(snapshot.Windows);
    }
}
