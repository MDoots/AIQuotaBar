namespace AIQuotaBar.Providers.Codex.Normalization;

using AIQuotaBar.Core.Models;
using AIQuotaBar.Core.Utils;
using AIQuotaBar.Providers.Codex.Protocol;

public static class CodexUsageNormalizer
{
    public static ProviderSnapshot Normalize(
        CodexRateLimitsResult? rateLimitsResult,
        CodexAccountResult? accountResult = null)
    {
        var plan = FormatPlanType(accountResult?.Account?.PlanType ?? rateLimitsResult?.RateLimits?.PlanType);

        if (rateLimitsResult == null)
        {
            if (accountResult?.RequiresOpenaiAuth == true && accountResult.Account == null)
            {
                return new ProviderSnapshot(
                    providerId: "codex",
                    providerDisplayName: "OpenAI Codex",
                    status: ProviderStatus.Unauthenticated,
                    statusMessage: "Codex account requires login",
                    accountPlan: plan);
            }

            return new ProviderSnapshot(
                providerId: "codex",
                providerDisplayName: "OpenAI Codex",
                status: ProviderStatus.Unavailable,
                statusMessage: "No rate limit data returned",
                accountPlan: plan);
        }

        var windows = new List<QuotaWindow>();

        // 1. Process primary and secondary from the main rateLimits snapshot
        var mainSnapshot = rateLimitsResult.RateLimits;
        if (mainSnapshot != null)
        {
            if (mainSnapshot.Primary != null && mainSnapshot.Primary.UsedPercent.HasValue)
            {
                windows.Add(CreateQuotaWindow("primary", mainSnapshot.Primary, "Primary Window"));
            }

            if (mainSnapshot.Secondary != null && mainSnapshot.Secondary.UsedPercent.HasValue)
            {
                windows.Add(CreateQuotaWindow("secondary", mainSnapshot.Secondary, "Secondary Window"));
            }
        }

        // 2. If main rateLimits was null or empty, check rateLimitsByLimitId
        if (windows.Count == 0 && rateLimitsResult.RateLimitsByLimitId != null)
        {
            foreach (var (limitId, snapshot) in rateLimitsResult.RateLimitsByLimitId)
            {
                if (snapshot.Primary != null && snapshot.Primary.UsedPercent.HasValue)
                {
                    windows.Add(CreateQuotaWindow($"{limitId}_primary", snapshot.Primary, $"{limitId} Primary"));
                }

                if (snapshot.Secondary != null && snapshot.Secondary.UsedPercent.HasValue)
                {
                    windows.Add(CreateQuotaWindow($"{limitId}_secondary", snapshot.Secondary, $"{limitId} Secondary"));
                }
            }
        }

        // 3. Determine status
        var status = windows.Count > 0 ? ProviderStatus.Available : ProviderStatus.Unavailable;
        var statusMessage = windows.Count > 0 ? null : "No active quota windows returned by Codex";

        return new ProviderSnapshot(
            providerId: "codex",
            providerDisplayName: "OpenAI Codex",
            status: status,
            statusMessage: statusMessage,
            accountPlan: plan,
            windows: windows);
    }

    private static QuotaWindow CreateQuotaWindow(
        string id,
        CodexRateLimitWindow window,
        string defaultLabel)
    {
        var rawUsedPercent = window.UsedPercent ?? 0;
        var duration = DurationFormatter.ToTimeSpan(window.WindowDurationMins);
        var displayName = DurationFormatter.FormatWindowName(window.WindowDurationMins, defaultLabel);
        
        DateTimeOffset? resetsAt = null;
        if (window.ResetsAt.HasValue && window.ResetsAt.Value > 0)
        {
            try
            {
                resetsAt = DateTimeOffset.FromUnixTimeSeconds(window.ResetsAt.Value);
            }
            catch
            {
                // Invalid epoch value
            }
        }

        var status = rawUsedPercent >= 100 
            ? QuotaWindowStatus.Exhausted 
            : QuotaWindowStatus.Active;

        return new QuotaWindow(
            id: id,
            displayName: displayName,
            rawUsedPercent: rawUsedPercent,
            duration: duration,
            resetsAt: resetsAt,
            status: status);
    }

    private static string? FormatPlanType(string? planType)
    {
        if (string.IsNullOrWhiteSpace(planType))
        {
            return null;
        }

        return planType.Trim().ToLowerInvariant() switch
        {
            "plus" => "ChatGPT Plus",
            "pro" => "ChatGPT Pro",
            "prolite" => "ChatGPT Pro Lite",
            "team" => "ChatGPT Team",
            "business" => "ChatGPT Business",
            "enterprise" => "ChatGPT Enterprise",
            "edu" => "ChatGPT Edu",
            "free" => "ChatGPT Free",
            "go" => "ChatGPT Go",
            _ => char.ToUpperInvariant(planType[0]) + planType[1..]
        };
    }
}
