namespace AIQuotaBar.Providers.GitHubCopilot.Tests;

using AIQuotaBar.Core.Models;
using AIQuotaBar.Providers.GitHubCopilot.Adapter;
using AIQuotaBar.Providers.GitHubCopilot.Normalization;
using Xunit;

public class CopilotUsageNormalizerTests
{
    [Fact]
    public void Normalize_WhenNull_ReturnsUnauthenticated()
    {
        var snapshot = CopilotUsageNormalizer.Normalize(null);

        Assert.Equal("github-copilot", snapshot.ProviderId);
        Assert.Equal("GitHub Copilot", snapshot.ProviderDisplayName);
        Assert.Equal(ProviderStatus.Unauthenticated, snapshot.Status);
        Assert.Equal("GitHub Copilot requires sign-in", snapshot.StatusMessage);
    }

    [Fact]
    public void Normalize_WhenUnauthenticated_ReturnsUnauthenticated()
    {
        var fetchResult = new CopilotFetchResult
        {
            AuthInfo = new CopilotAuthInfoDto
            {
                IsAuthenticated = false,
                Plan = "individual"
            }
        };

        var snapshot = CopilotUsageNormalizer.Normalize(fetchResult);

        Assert.Equal(ProviderStatus.Unauthenticated, snapshot.Status);
        Assert.Equal("GitHub Copilot requires sign-in", snapshot.StatusMessage);
        Assert.Equal("Copilot Individual", snapshot.AccountPlan);
    }

    [Fact]
    public void Normalize_WhenSubscriptionEnded_ReturnsUnavailable()
    {
        var fetchResult = new CopilotFetchResult
        {
            AuthInfo = new CopilotAuthInfoDto
            {
                IsAuthenticated = true,
                AccessTypeSku = "subscription_ended",
                Plan = "individual"
            }
        };

        var snapshot = CopilotUsageNormalizer.Normalize(fetchResult);

        Assert.Equal(ProviderStatus.Unavailable, snapshot.Status);
        Assert.Equal("Copilot subscription has ended", snapshot.StatusMessage);
        Assert.Equal("Copilot Individual", snapshot.AccountPlan);
    }

    [Fact]
    public void Normalize_WithFinitePremiumQuota_NormalizesCorrectly()
    {
        var resetDate = DateTimeOffset.UtcNow.AddDays(14);
        var fetchResult = new CopilotFetchResult
        {
            AuthInfo = new CopilotAuthInfoDto
            {
                IsAuthenticated = true,
                Plan = "individual",
                Login = "MDoots"
            },
            Quotas = new[]
            {
                new CopilotQuotaDto
                {
                    Key = "premium_interactions",
                    EntitlementRequests = 300,
                    IsUnlimitedEntitlement = false,
                    RemainingPercentage = 68.0,
                    ResetDate = resetDate
                },
                // Unlimited quota that should be filtered out
                new CopilotQuotaDto
                {
                    Key = "standard_interactions",
                    EntitlementRequests = -1,
                    IsUnlimitedEntitlement = true,
                    RemainingPercentage = 100.0
                }
            }
        };

        var snapshot = CopilotUsageNormalizer.Normalize(fetchResult);

        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Equal("Copilot Individual", snapshot.AccountPlan);
        Assert.Null(snapshot.StatusMessage);
        Assert.Single(snapshot.Windows);

        var window = snapshot.Windows[0];
        Assert.Equal("premium", window.Id);
        Assert.Equal("Premium", window.DisplayName);
        Assert.Equal(32.0, window.RawUsedPercent);
        Assert.Equal(68.0, window.RemainingPercent);
        Assert.Equal(resetDate, window.ResetsAt);
        Assert.Equal(QuotaWindowStatus.Active, window.Status);
    }

    [Fact]
    public void Normalize_WithDuplicatePremiumAliases_EmitsSingleCanonicalRow()
    {
        var resetDate = DateTimeOffset.UtcNow.AddDays(14);
        var fetchResult = new CopilotFetchResult
        {
            AuthInfo = new CopilotAuthInfoDto
            {
                IsAuthenticated = true,
                Plan = "enterprise"
            },
            Quotas = new[]
            {
                new CopilotQuotaDto
                {
                    Key = "premium",
                    EntitlementRequests = 500,
                    IsUnlimitedEntitlement = false,
                    RemainingPercentage = 75.0,
                    ResetDate = resetDate
                },
                new CopilotQuotaDto
                {
                    Key = "premium_interactions",
                    EntitlementRequests = 500,
                    IsUnlimitedEntitlement = false,
                    RemainingPercentage = 75.0,
                    ResetDate = resetDate
                }
            }
        };

        var snapshot = CopilotUsageNormalizer.Normalize(fetchResult);

        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Single(snapshot.Windows); // Deduplicated!
        Assert.Equal("premium", snapshot.Windows[0].Id);
        Assert.Equal("Premium", snapshot.Windows[0].DisplayName);
        Assert.Equal(75.0, snapshot.Windows[0].RemainingPercent);
    }

    [Fact]
    public void Normalize_WithNullReset_ParsesSafely()
    {
        var fetchResult = new CopilotFetchResult
        {
            AuthInfo = new CopilotAuthInfoDto
            {
                IsAuthenticated = true,
                Plan = "pro"
            },
            Quotas = new[]
            {
                new CopilotQuotaDto
                {
                    Key = "premium",
                    EntitlementRequests = 100,
                    IsUnlimitedEntitlement = false,
                    RemainingPercentage = 50.0,
                    ResetDate = null
                }
            }
        };

        var snapshot = CopilotUsageNormalizer.Normalize(fetchResult);

        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Single(snapshot.Windows);
        Assert.Null(snapshot.Windows[0].ResetsAt);
    }

    [Fact]
    public void Normalize_WhenQuotaExhausted_SetsExhaustedStatus()
    {
        var fetchResult = new CopilotFetchResult
        {
            AuthInfo = new CopilotAuthInfoDto
            {
                IsAuthenticated = true,
                Plan = "business"
            },
            Quotas = new[]
            {
                new CopilotQuotaDto
                {
                    Key = "premium",
                    EntitlementRequests = 100,
                    IsUnlimitedEntitlement = false,
                    RemainingPercentage = 0.0
                }
            }
        };

        var snapshot = CopilotUsageNormalizer.Normalize(fetchResult);

        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Equal("Copilot Business", snapshot.AccountPlan);
        Assert.Single(snapshot.Windows);
        Assert.Equal(100.0, snapshot.Windows[0].RawUsedPercent);
        Assert.Equal(0.0, snapshot.Windows[0].RemainingPercent);
        Assert.Equal(QuotaWindowStatus.Exhausted, snapshot.Windows[0].Status);
    }

    [Fact]
    public void Normalize_WhenFreePlanWithFiniteQuota_ReturnsAvailable()
    {
        var fetchResult = new CopilotFetchResult
        {
            AuthInfo = new CopilotAuthInfoDto
            {
                IsAuthenticated = true,
                Plan = "free"
            },
            Quotas = new[]
            {
                new CopilotQuotaDto
                {
                    Key = "premium",
                    EntitlementRequests = 50,
                    IsUnlimitedEntitlement = false,
                    RemainingPercentage = 90.0,
                    ResetDate = DateTimeOffset.UtcNow.AddDays(7)
                }
            }
        };

        var snapshot = CopilotUsageNormalizer.Normalize(fetchResult);

        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Equal("Copilot Free", snapshot.AccountPlan);
        Assert.Single(snapshot.Windows);
        Assert.Equal(90.0, snapshot.Windows[0].RemainingPercent);
    }

    [Fact]
    public void Normalize_WhenSubscriptionEnded_WithFiniteQuota_ReturnsAvailable()
    {
        var fetchResult = new CopilotFetchResult
        {
            AuthInfo = new CopilotAuthInfoDto
            {
                IsAuthenticated = true,
                AccessTypeSku = "subscription_ended",
                Plan = "individual"
            },
            Quotas = new[]
            {
                new CopilotQuotaDto
                {
                    Key = "premium",
                    EntitlementRequests = 50,
                    IsUnlimitedEntitlement = false,
                    RemainingPercentage = 100.0
                }
            }
        };

        var snapshot = CopilotUsageNormalizer.Normalize(fetchResult);

        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Single(snapshot.Windows);
    }

    [Fact]
    public void Normalize_WhenAuthenticatedWithNoFiniteQuotaAndOtherStatus_ReturnsTruthfulNoQuota()
    {
        var fetchResult = new CopilotFetchResult
        {
            AuthInfo = new CopilotAuthInfoDto
            {
                IsAuthenticated = true,
                AccessTypeSku = "active",
                Plan = "individual"
            },
            Quotas = Array.Empty<CopilotQuotaDto>()
        };

        var snapshot = CopilotUsageNormalizer.Normalize(fetchResult);

        Assert.Equal(ProviderStatus.Unavailable, snapshot.Status);
        Assert.Equal("Copilot quota is not available from the local CLI", snapshot.StatusMessage);
    }
}
