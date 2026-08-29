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

        if (fetchResult?.Quotas != null && fetchResult.Quotas.Count > 0)
        {
            // Filter to finite entitlements only
            var finiteQuotas = fetchResult.Quotas
                .Where(q => !q.IsUnlimitedEntitlement && q.EntitlementRequests != -1)
                .ToList();

            // Map and deduplicate understood quota keys
            // 1. Premium interactions (prefer canonical "premium_interactions" over legacy alias "premium")
            var premiumQuota = finiteQuotas.FirstOrDefault(q => string.Equals(q.Key, "premium_interactions", StringComparison.OrdinalIgnoreCase))
                ?? finiteQuotas.FirstOrDefault(q => string.Equals(q.Key, "premium", StringComparison.OrdinalIgnoreCase));

            if (premiumQuota != null)
            {
                var clampedRemaining = Math.Clamp(premiumQuota.RemainingPercentage, 0.0, 100.0);
                var rawUsed = Math.Clamp(100.0 - clampedRemaining, 0.0, 100.0);
                var status = rawUsed >= 100.0 ? QuotaWindowStatus.Exhausted : QuotaWindowStatus.Active;

                windows.Add(new QuotaWindow(
                    id: "premium",
                    displayName: "Premium interactions",
                    rawUsedPercent: rawUsed,
                    duration: null,
                    resetsAt: premiumQuota.ResetDate,
                    status: status));
            }

            // 2. Chat (only if finite)
            var chatQuota = finiteQuotas.FirstOrDefault(q => string.Equals(q.Key, "chat", StringComparison.OrdinalIgnoreCase));
            if (chatQuota != null)
            {
                var clampedRemaining = Math.Clamp(chatQuota.RemainingPercentage, 0.0, 100.0);
                var rawUsed = Math.Clamp(100.0 - clampedRemaining, 0.0, 100.0);
                var status = rawUsed >= 100.0 ? QuotaWindowStatus.Exhausted : QuotaWindowStatus.Active;

                windows.Add(new QuotaWindow(
                    id: "chat",
                    displayName: "Chat",
                    rawUsedPercent: rawUsed,
                    duration: null,
                    resetsAt: chatQuota.ResetDate,
                    status: status));
            }

            // 3. Completions (only if finite)
            var completionsQuota = finiteQuotas.FirstOrDefault(q => string.Equals(q.Key, "completions", StringComparison.OrdinalIgnoreCase));
            if (completionsQuota != null)
            {
                var clampedRemaining = Math.Clamp(completionsQuota.RemainingPercentage, 0.0, 100.0);
                var rawUsed = Math.Clamp(100.0 - clampedRemaining, 0.0, 100.0);
                var status = rawUsed >= 100.0 ? QuotaWindowStatus.Exhausted : QuotaWindowStatus.Active;

                windows.Add(new QuotaWindow(
                    id: "completions",
                    displayName: "Completions",
                    rawUsedPercent: rawUsed,
                    duration: null,
                    resetsAt: completionsQuota.ResetDate,
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
