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

        // A populated dictionary is authoritative. It contains the per-pool view and
        // must not be supplemented with an older aggregate snapshot.
        if (rateLimitsResult.RateLimitsByLimitId is { Count: > 0 } byLimitId)
        {
            foreach (var entry in byLimitId.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                var limitId = string.IsNullOrWhiteSpace(entry.Key) ? "limit" : entry.Key.Trim();
                var snapshot = entry.Value;
                if (snapshot is null)
                {
                    continue;
                }

                var poolLabel = string.IsNullOrWhiteSpace(snapshot.LimitName) ? limitId : snapshot.LimitName;
                AddWindow(windows, $"{limitId}_primary", snapshot.Primary, poolLabel, "Primary");
                AddWindow(windows, $"{limitId}_secondary", snapshot.Secondary, poolLabel, "Secondary");
            }
        }
        else if (rateLimitsResult.RateLimits is { } mainSnapshot)
        {
            // Legacy responses have only one pool, so retain the stable historic IDs.
            AddWindow(windows, "primary", mainSnapshot.Primary, null, "Primary Window");
            AddWindow(windows, "secondary", mainSnapshot.Secondary, null, "Secondary Window");
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

    private static void AddWindow(
        List<QuotaWindow> windows,
        string id,
        CodexRateLimitWindow? window,
        string? limitName,
        string roleLabel)
    {
        if (window != null)
        {
            if (!window.UsedPercent.HasValue)
            {
                return;
            }

            windows.Add(CreateQuotaWindow(id, window, limitName, roleLabel));
        }
    }

    private static QuotaWindow CreateQuotaWindow(
        string id,
        CodexRateLimitWindow window,
        string? limitName,
        string roleLabel)
    {
        var rawUsedPercent = Math.Clamp(window.UsedPercent!.Value, 0, 100);
        var duration = DurationFormatter.ToTimeSpan(window.WindowDurationMins);
        var durationLabel = DurationFormatter.FormatWindowName(window.WindowDurationMins, roleLabel);
        var displayName = string.IsNullOrWhiteSpace(limitName)
            ? durationLabel
            : $"{limitName.Trim()} · {durationLabel}";
        
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
