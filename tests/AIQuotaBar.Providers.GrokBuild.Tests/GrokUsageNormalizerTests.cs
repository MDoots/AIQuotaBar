namespace AIQuotaBar.Providers.GrokBuild.Tests;

using AIQuotaBar.Core.Models;
using AIQuotaBar.Providers.GrokBuild.Normalization;
using AIQuotaBar.Providers.GrokBuild.Protocol;
using Xunit;

public class GrokUsageNormalizerTests
{
    [Fact]
    public void Normalize_WithNullResult_ReturnsUnavailable()
    {
        var snapshot = GrokUsageNormalizer.Normalize(null);

        Assert.Equal("grok-build", snapshot.ProviderId);
        Assert.Equal("Grok Build", snapshot.ProviderDisplayName);
        Assert.Equal(ProviderStatus.Unavailable, snapshot.Status);
        Assert.Equal("No billing data returned by Grok", snapshot.StatusMessage);
        Assert.Empty(snapshot.Windows);
    }

    [Fact]
    public void Normalize_WithNullConfig_ReturnsUnavailableWithPlan()
    {
        var result = new GrokBillingResult
        {
            SubscriptionTier = "SuperGrok"
        };

        var snapshot = GrokUsageNormalizer.Normalize(result);

        Assert.Equal(ProviderStatus.Unavailable, snapshot.Status);
        Assert.Equal("SuperGrok", snapshot.AccountPlan);
    }

    [Fact]
    public void Normalize_UnifiedWeeklyBilling_NormalizesCorrectly()
    {
        var result = new GrokBillingResult
        {
            SubscriptionTier = "Free",
            Config = new GrokBillingConfig
            {
                IsUnifiedBillingUser = true,
                CurrentPeriod = new GrokCurrentPeriod
                {
                    Type = "USAGE_PERIOD_TYPE_WEEKLY",
                    Start = "2026-08-26T00:00:00+00:00",
                    End = "2026-09-02T00:00:00+00:00"
                },
                CreditUsagePercent = 25.5
            }
        };

        var snapshot = GrokUsageNormalizer.Normalize(result);

        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Equal("Free", snapshot.AccountPlan);
        Assert.Single(snapshot.Windows);

        var window = snapshot.Windows[0];
        Assert.Equal("shared-weekly", window.Id);
        Assert.Equal("Grok · Weekly", window.DisplayName);
        Assert.Equal(25.5, window.RawUsedPercent);
        Assert.Equal(74.5, window.RemainingPercent);
        Assert.Equal(TimeSpan.FromDays(7), window.Duration);
        Assert.Equal(DateTimeOffset.Parse("2026-09-02T00:00:00+00:00"), window.ResetsAt);
        Assert.Equal(QuotaWindowStatus.Active, window.Status);
    }

    [Fact]
    public void Normalize_UnifiedMonthlyBilling_NormalizesCorrectly()
    {
        var result = new GrokBillingResult
        {
            SubscriptionTier = "Team",
            Config = new GrokBillingConfig
            {
                IsUnifiedBillingUser = true,
                CurrentPeriod = new GrokCurrentPeriod
                {
                    Type = "USAGE_PERIOD_TYPE_MONTHLY",
                    End = "2026-09-30T23:59:59+00:00"
                },
                CreditUsagePercent = 100.0
            }
        };

        var snapshot = GrokUsageNormalizer.Normalize(result);

        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Equal("Team", snapshot.AccountPlan);
        Assert.Single(snapshot.Windows);

        var window = snapshot.Windows[0];
        Assert.Equal("shared-monthly", window.Id);
        Assert.Equal("Grok · Monthly", window.DisplayName);
        Assert.Equal(100.0, window.RawUsedPercent);
        Assert.Equal(0.0, window.RemainingPercent);
        Assert.Equal(TimeSpan.FromDays(30), window.Duration);
        Assert.Equal(QuotaWindowStatus.Exhausted, window.Status);
    }

    [Fact]
    public void Normalize_MissingPercentage_EmitsNoQuota_NeverFabricates100Percent()
    {
        var result = new GrokBillingResult
        {
            SubscriptionTier = "Custom",
            Config = new GrokBillingConfig
            {
                IsUnifiedBillingUser = true,
                CurrentPeriod = new GrokCurrentPeriod
                {
                    Type = "weekly",
                    End = "2026-09-02T00:00:00Z"
                },
                CreditUsagePercent = null, // Missing!
                Used = null,
                MonthlyLimit = null
            }
        };

        var snapshot = GrokUsageNormalizer.Normalize(result);

        Assert.Equal(ProviderStatus.Unavailable, snapshot.Status);
        Assert.Equal("No finite quota returned by Grok", snapshot.StatusMessage);
        Assert.Empty(snapshot.Windows);
    }

    [Fact]
    public void Normalize_UnknownPeriodType_EmitsNoGuessedQuota()
    {
        var result = new GrokBillingResult
        {
            SubscriptionTier = "Pro",
            Config = new GrokBillingConfig
            {
                CurrentPeriod = new GrokCurrentPeriod
                {
                    Type = "UNKNOWN_CUSTOM_FUTURE_PERIOD_TYPE",
                    End = "2026-09-15T00:00:00Z"
                },
                CreditUsagePercent = 30.0
            }
        };

        var snapshot = GrokUsageNormalizer.Normalize(result);

        Assert.Equal(ProviderStatus.Unavailable, snapshot.Status);
        Assert.Equal("Unrecognized billing period from Grok", snapshot.StatusMessage);
        Assert.Empty(snapshot.Windows);
    }

    [Fact]
    public void Normalize_BiweeklyUnknownPeriod_EmitsNoQuotaRow()
    {
        var result = new GrokBillingResult
        {
            SubscriptionTier = "Pro",
            Config = new GrokBillingConfig
            {
                CurrentPeriod = new GrokCurrentPeriod
                {
                    Type = "BIWEEKLY", // Contains "WEEKLY" but is NOT weekly
                    End = "2026-09-15T00:00:00Z"
                },
                CreditUsagePercent = 30.0
            }
        };

        var snapshot = GrokUsageNormalizer.Normalize(result);

        Assert.Equal(ProviderStatus.Unavailable, snapshot.Status);
        Assert.Equal("Unrecognized billing period from Grok", snapshot.StatusMessage);
        Assert.Empty(snapshot.Windows);
    }

    [Fact]
    public void Normalize_RollingMonthlyUnknownPeriod_EmitsNoQuotaRow()
    {
        var result = new GrokBillingResult
        {
            SubscriptionTier = "Pro",
            Config = new GrokBillingConfig
            {
                CurrentPeriod = new GrokCurrentPeriod
                {
                    Type = "SOME_FUTURE_MONTHLY_ROLLING_TYPE", // Contains "MONTHLY" but is not standard monthly
                    End = "2026-09-15T00:00:00Z"
                },
                CreditUsagePercent = 30.0
            }
        };

        var snapshot = GrokUsageNormalizer.Normalize(result);

        Assert.Equal(ProviderStatus.Unavailable, snapshot.Status);
        Assert.Equal("Unrecognized billing period from Grok", snapshot.StatusMessage);
        Assert.Empty(snapshot.Windows);
    }

    [Fact]
    public void Normalize_LegacyBuildSpecificBilling_NormalizesCorrectly()
    {
        var result = new GrokBillingResult
        {
            SubscriptionTier = "Enterprise",
            Config = new GrokBillingConfig
            {
                IsUnifiedBillingUser = false,
                CurrentPeriod = new GrokCurrentPeriod
                {
                    Type = "USAGE_PERIOD_TYPE_MONTHLY"
                },
                Used = new GrokNumericVal { Val = 50 },
                MonthlyLimit = new GrokNumericVal { Val = 200 },
                BillingPeriodEnd = "2026-10-01T00:00:00Z"
            }
        };

        var snapshot = GrokUsageNormalizer.Normalize(result);

        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Single(snapshot.Windows);

        var window = snapshot.Windows[0];
        Assert.Equal("build-monthly", window.Id);
        Assert.Equal("Build · Monthly", window.DisplayName);
        Assert.Equal(25.0, window.RawUsedPercent);
        Assert.Equal(75.0, window.RemainingPercent);
        Assert.Equal(TimeSpan.FromDays(30), window.Duration);
    }

    [Fact]
    public void Normalize_FreeTierWithoutFiniteQuota_ReturnsUnavailableWithPlanAndNoWindows()
    {
        var result = new GrokBillingResult
        {
            SubscriptionTier = "Free",
            Config = new GrokBillingConfig
            {
                IsUnifiedBillingUser = true,
                CurrentPeriod = new GrokCurrentPeriod
                {
                    Type = "USAGE_PERIOD_TYPE_WEEKLY",
                    Start = "2026-08-26T01:00:00+00:00",
                    End = "2026-09-02T01:00:00+00:00"
                },
                BillingPeriodStart = "2026-08-26T01:00:00+00:00",
                BillingPeriodEnd = "2026-09-02T01:00:00+00:00",
                CreditUsagePercent = null,
                Used = null,
                MonthlyLimit = null
            }
        };

        var snapshot = GrokUsageNormalizer.Normalize(result);

        Assert.Equal(ProviderStatus.Unavailable, snapshot.Status);
        Assert.Equal("No finite quota returned by Grok", snapshot.StatusMessage);
        Assert.Equal("Free", snapshot.AccountPlan);
        Assert.Empty(snapshot.Windows);
    }
}
