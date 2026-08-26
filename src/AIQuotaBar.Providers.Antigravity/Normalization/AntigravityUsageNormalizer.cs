namespace AIQuotaBar.Providers.Antigravity.Normalization;

using System.Globalization;
using AIQuotaBar.Core.Models;
using AIQuotaBar.Providers.Antigravity.Protocol;

public static class AntigravityUsageNormalizer
{
    public const string ProviderIdentifier = "antigravity";
    public const string ProviderDisplayName = "Google Antigravity";

    public static ProviderSnapshot Normalize(AntigravityCliResponse? cliResponse)
    {
        if (cliResponse == null)
        {
            return new ProviderSnapshot(
                providerId: ProviderIdentifier,
                providerDisplayName: ProviderDisplayName,
                status: ProviderStatus.Unavailable,
                statusMessage: "No response received from Antigravity CLI");
        }

        // Check for failure status
        if (!string.IsNullOrWhiteSpace(cliResponse.Status) &&
            !string.Equals(cliResponse.Status, "SUCCESS", StringComparison.OrdinalIgnoreCase))
        {
            var rawErr = $"{cliResponse.Error} {cliResponse.Response}".Trim();
            if (rawErr.Contains("auth", StringComparison.OrdinalIgnoreCase) ||
                rawErr.Contains("login", StringComparison.OrdinalIgnoreCase) ||
                rawErr.Contains("unauthenticated", StringComparison.OrdinalIgnoreCase) ||
                rawErr.Contains("not logged in", StringComparison.OrdinalIgnoreCase))
            {
                return new ProviderSnapshot(
                    providerId: ProviderIdentifier,
                    providerDisplayName: ProviderDisplayName,
                    status: ProviderStatus.Unauthenticated,
                    statusMessage: "Antigravity CLI requires authentication");
            }

            return new ProviderSnapshot(
                providerId: ProviderIdentifier,
                providerDisplayName: ProviderDisplayName,
                status: ProviderStatus.Error,
                statusMessage: "Antigravity CLI returned an error");
        }

        var groups = cliResponse.Command?.Data?.Groups;
        if (groups == null || groups.Count == 0)
        {
            return new ProviderSnapshot(
                providerId: ProviderIdentifier,
                providerDisplayName: ProviderDisplayName,
                status: ProviderStatus.Unavailable,
                statusMessage: "No active quota windows returned by Antigravity");
        }

        var windows = new List<QuotaWindow>();

        foreach (var group in groups)
        {
            var groupPrefix = FormatGroupName(group.Name);
            if (group.Buckets == null)
            {
                continue;
            }

            foreach (var bucket in group.Buckets)
            {
                var window = CreateQuotaWindow(groupPrefix, bucket);
                if (window != null)
                {
                    windows.Add(window);
                }
            }
        }

        if (windows.Count == 0)
        {
            return new ProviderSnapshot(
                providerId: ProviderIdentifier,
                providerDisplayName: ProviderDisplayName,
                status: ProviderStatus.Unavailable,
                statusMessage: "No active quota windows returned by Antigravity");
        }

        // Order windows logically: 5h before weekly, Gemini before Claude/GPT
        var orderedWindows = windows
            .OrderBy(w => GetGroupSortOrder(w.DisplayName))
            .ThenBy(w => GetWindowSortOrder(w.DisplayName))
            .ToList();

        return new ProviderSnapshot(
            providerId: ProviderIdentifier,
            providerDisplayName: ProviderDisplayName,
            status: ProviderStatus.Available,
            statusMessage: null,
            accountPlan: null, // Left null unless verified official plan metadata is present
            windows: orderedWindows);
    }

    private static QuotaWindow? CreateQuotaWindow(string groupPrefix, AntigravityBucket bucket)
    {
        var rawFraction = bucket.RemainingFraction;
        if (!rawFraction.HasValue || double.IsNaN(rawFraction.Value) || double.IsInfinity(rawFraction.Value))
        {
            return null;
        }

        // Safely clamp remaining fraction to [0.0, 1.0] and compute raw used percent with full precision
        var clampedFraction = Math.Clamp(rawFraction.Value, 0.0, 1.0);
        var rawUsedPercent = 100.0 * (1.0 - clampedFraction);

        var (windowSuffix, duration) = FormatWindowType(bucket.Window, bucket.Name, bucket.Id);
        var displayName = $"{groupPrefix} · {windowSuffix}";
        var id = $"{groupPrefix.ToLowerInvariant().Replace(" ", "_").Replace("&", "and")}_{bucket.Id ?? bucket.Window ?? windowSuffix.ToLowerInvariant()}";

        DateTimeOffset? resetsAt = null;
        if (!string.IsNullOrWhiteSpace(bucket.ResetTime))
        {
            if (DateTimeOffset.TryParse(bucket.ResetTime, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            {
                resetsAt = parsed.ToUniversalTime();
            }
        }

        var status = rawUsedPercent >= 100.0
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

    private static string FormatGroupName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "General";
        }

        var trimmed = name.Trim();
        if (trimmed.Contains("gemini", StringComparison.OrdinalIgnoreCase))
        {
            return "Gemini";
        }

        if (trimmed.Contains("claude", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("gpt", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("3p", StringComparison.OrdinalIgnoreCase))
        {
            return "Claude & GPT";
        }

        return trimmed;
    }

    private static (string Suffix, TimeSpan? Duration) FormatWindowType(string? window, string? name, string? id)
    {
        var combined = $"{window} {name} {id}".ToLowerInvariant();

        if (combined.Contains("5h") || combined.Contains("five hour") || combined.Contains("5 hour"))
        {
            return ("5-Hour", TimeSpan.FromHours(5));
        }

        if (combined.Contains("weekly") || combined.Contains("week") || combined.Contains("7d"))
        {
            return ("Weekly", TimeSpan.FromDays(7));
        }

        if (!string.IsNullOrWhiteSpace(window))
        {
            return (char.ToUpperInvariant(window[0]) + window[1..], null);
        }

        return ("Limit", null);
    }

    private static int GetGroupSortOrder(string displayName)
    {
        if (displayName.StartsWith("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (displayName.StartsWith("Claude", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 3;
    }

    private static int GetWindowSortOrder(string displayName)
    {
        if (displayName.Contains("5-Hour", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (displayName.Contains("Weekly", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 3;
    }
}
