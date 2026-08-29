namespace AIQuotaBar.Providers.GitHubCopilot.Normalization;

using AIQuotaBar.Core.Models;
using AIQuotaBar.Providers.GitHubCopilot.Adapter;

public static class CopilotUsageNormalizer
{
    public const string ProviderIdentifier = "github-copilot";
    public const string ProviderName = "GitHub Copilot";

    public static ProviderSnapshot Normalize(CopilotFetchResult? fetchResult)
    {
        var authInfo = fetchResult?.AuthInfo;
        var plan = FormatPlan(authInfo?.Plan);

        if (authInfo == null || !authInfo.IsAuthenticated)
        {
            return new ProviderSnapshot(
                providerId: ProviderIdentifier,
                providerDisplayName: ProviderName,
                status: ProviderStatus.Unauthenticated,
                statusMessage: "GitHub Copilot requires sign-in",
                accountPlan: plan);
        }

        var windows = new List<QuotaWindow>();

        if (fetchResult?.Quotas != null)
        {
            var seenWindowIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var q in fetchResult.Quotas)
            {
                // Skip unlimited quota entitlements
                if (q.IsUnlimitedEntitlement || q.EntitlementRequests == -1)
                {
                    continue;
                }

                var windowId = FormatWindowId(q.Key);
                if (!seenWindowIds.Add(windowId))
                {
                    continue; // Deduplicate alias keys
                }

                var rawUsedPercent = Math.Clamp(100.0 - q.RemainingPercentage, 0.0, 100.0);
                var displayName = FormatWindowDisplayName(q.Key);
                var status = rawUsedPercent >= 100 ? QuotaWindowStatus.Exhausted : QuotaWindowStatus.Active;

                windows.Add(new QuotaWindow(
                    id: windowId,
                    displayName: displayName,
                    rawUsedPercent: rawUsedPercent,
                    duration: TimeSpan.FromDays(30),
                    resetsAt: q.ResetDate,
                    status: status));
            }
        }

        if (windows.Count > 0)
        {
            return new ProviderSnapshot(
                providerId: ProviderIdentifier,
                providerDisplayName: ProviderName,
                status: ProviderStatus.Available,
                statusMessage: null,
                accountPlan: plan,
                windows: windows);
        }

        if (string.Equals(authInfo.AccessTypeSku, "subscription_ended", StringComparison.OrdinalIgnoreCase))
        {
            return new ProviderSnapshot(
                providerId: ProviderIdentifier,
                providerDisplayName: ProviderName,
                status: ProviderStatus.Unavailable,
                statusMessage: "Copilot subscription has ended",
                accountPlan: plan);
        }

        return new ProviderSnapshot(
            providerId: ProviderIdentifier,
            providerDisplayName: ProviderName,
            status: ProviderStatus.Unavailable,
            statusMessage: "Copilot quota is not available from the local CLI",
            accountPlan: plan);
    }

    private static string FormatWindowId(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return "quota";
        }

        var lower = key.ToLowerInvariant();
        if (lower.Contains("premium"))
        {
            return "premium";
        }

        return lower.Replace('_', '-');
    }

    private static string FormatWindowDisplayName(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return "Monthly Quota";
        }

        var lower = key.ToLowerInvariant();
        if (lower.Contains("premium"))
        {
            return "Premium";
        }

        return key;
    }

    private static string? FormatPlan(string? plan)
    {
        if (string.IsNullOrWhiteSpace(plan))
        {
            return null;
        }

        return plan.Trim().ToLowerInvariant() switch
        {
            "individual" => "Copilot Individual",
            "business" => "Copilot Business",
            "enterprise" => "Copilot Enterprise",
            "pro" => "Copilot Pro",
            "free" => "Copilot Free",
            _ => "Copilot " + char.ToUpperInvariant(plan[0]) + plan[1..]
        };
    }
}
