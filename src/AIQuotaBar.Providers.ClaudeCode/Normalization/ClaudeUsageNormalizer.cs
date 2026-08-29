namespace AIQuotaBar.Providers.ClaudeCode.Normalization;

using System.Globalization;
using System.Text.RegularExpressions;
using AIQuotaBar.Core.Models;

public static partial class ClaudeUsageNormalizer
{
    public const string ProviderIdentifier = "claude-code";
    public const string ProviderName = "Claude Code";

    [GeneratedRegex(@"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])", RegexOptions.Compiled)]
    private static partial Regex AnsiRegex();

    [GeneratedRegex(@"(\d+(?:\.\d+)?)\s*%\s*(?:used|consumed)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex UsedPercentRegex();

    [GeneratedRegex(@"(?:used|consumed)\s*[:=]\s*(\d+(?:\.\d+)?)\s*%", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex UsedColonPercentRegex();

    [GeneratedRegex(@"resets?\s+(?:in\s+)?([0-9]+\s*(?:h|hr|hours?|m|min|minutes?|d|days?)(?:\s+[0-9]+\s*(?:m|min|minutes?))?)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex RelativeResetRegex();

    [GeneratedRegex(@"resets?\s+(?:at\s+)?([0-9]{1,2}:[0-9]{2}(?:\s*[ap]m)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex AbsoluteTimeResetRegex();

    public static ProviderSnapshot Normalize(string? rawOutput, string? plan = null)
    {
        if (string.IsNullOrWhiteSpace(rawOutput))
        {
            return new ProviderSnapshot(
                providerId: ProviderIdentifier,
                providerDisplayName: ProviderName,
                status: ProviderStatus.Unavailable,
                statusMessage: "No usage data returned by Claude Code",
                accountPlan: plan);
        }

        var cleaned = AnsiRegex().Replace(rawOutput, " ");

        if (cleaned.Contains("not logged in", StringComparison.OrdinalIgnoreCase) ||
            cleaned.Contains("please run /login", StringComparison.OrdinalIgnoreCase) ||
            cleaned.Contains("run `claude login`", StringComparison.OrdinalIgnoreCase) ||
            cleaned.Contains("authentication required", StringComparison.OrdinalIgnoreCase))
        {
            return new ProviderSnapshot(
                providerId: ProviderIdentifier,
                providerDisplayName: ProviderName,
                status: ProviderStatus.Unauthenticated,
                statusMessage: "Claude Code requires sign-in",
                accountPlan: plan);
        }

        var lines = cleaned.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var windows = new List<QuotaWindow>();

        foreach (var line in lines)
        {
            var match = UsedPercentRegex().Match(line);
            if (!match.Success)
            {
                match = UsedColonPercentRegex().Match(line);
            }

            if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var usedVal))
            {
                var rawUsedPercent = Math.Clamp(usedVal, 0.0, 100.0);
                var lower = line.ToLowerInvariant();

                string windowId;
                string displayName;
                TimeSpan duration;

                if (lower.Contains("opus"))
                {
                    windowId = "weekly-opus";
                    displayName = "Opus · Weekly";
                    duration = TimeSpan.FromDays(7);
                }
                else if (lower.Contains("sonnet"))
                {
                    windowId = "weekly-sonnet";
                    displayName = "Sonnet · Weekly";
                    duration = TimeSpan.FromDays(7);
                }
                else if (lower.Contains("session") || lower.Contains("5-hour") || lower.Contains("5h") || lower.Contains("hourly"))
                {
                    windowId = "session-5h";
                    displayName = "5-Hour Session";
                    duration = TimeSpan.FromHours(5);
                }
                else if (lower.Contains("weekly") || lower.Contains("week") || lower.Contains("7-day"))
                {
                    windowId = "weekly-all";
                    displayName = "Weekly";
                    duration = TimeSpan.FromDays(7);
                }
                else
                {
                    windowId = $"window-{windows.Count + 1}";
                    displayName = "Allowance";
                    duration = TimeSpan.FromHours(5);
                }

                // Avoid duplicate window IDs
                if (windows.Any(w => w.Id == windowId))
                {
                    windowId = $"{windowId}-{windows.Count + 1}";
                }

                var resetsAt = ParseResetTime(line);
                var status = rawUsedPercent >= 100 ? QuotaWindowStatus.Exhausted : QuotaWindowStatus.Active;

                windows.Add(new QuotaWindow(
                    id: windowId,
                    displayName: displayName,
                    rawUsedPercent: rawUsedPercent,
                    duration: duration,
                    resetsAt: resetsAt,
                    status: status));
            }
        }

        if (windows.Count == 0)
        {
            if (cleaned.Contains("api key", StringComparison.OrdinalIgnoreCase) ||
                cleaned.Contains("usage-based", StringComparison.OrdinalIgnoreCase) ||
                cleaned.Contains("pay-as-you-go", StringComparison.OrdinalIgnoreCase) ||
                cleaned.Contains("no subscription", StringComparison.OrdinalIgnoreCase))
            {
                return new ProviderSnapshot(
                    providerId: ProviderIdentifier,
                    providerDisplayName: ProviderName,
                    status: ProviderStatus.Available,
                    statusMessage: "Usage-based billing — no fixed Claude Code quota",
                    accountPlan: plan,
                    windows: windows);
            }

            return new ProviderSnapshot(
                providerId: ProviderIdentifier,
                providerDisplayName: ProviderName,
                status: ProviderStatus.Unavailable,
                statusMessage: "Quota is not available from the local provider",
                accountPlan: plan,
                windows: windows);
        }

        return new ProviderSnapshot(
            providerId: ProviderIdentifier,
            providerDisplayName: ProviderName,
            status: ProviderStatus.Available,
            statusMessage: null,
            accountPlan: plan,
            windows: windows);
    }

    private static DateTimeOffset? ParseResetTime(string line)
    {
        var relMatch = RelativeResetRegex().Match(line);
        if (relMatch.Success)
        {
            var relText = relMatch.Groups[1].Value.ToLowerInvariant();
            var days = 0;
            var hours = 0;
            var minutes = 0;

            var dMatch = Regex.Match(relText, @"(\d+)\s*(?:d|days?)");
            if (dMatch.Success && int.TryParse(dMatch.Groups[1].Value, out var d))
            {
                days = d;
            }

            var hMatch = Regex.Match(relText, @"(\d+)\s*(?:h|hr|hours?)");
            if (hMatch.Success && int.TryParse(hMatch.Groups[1].Value, out var h))
            {
                hours = h;
            }

            var mMatch = Regex.Match(relText, @"(\d+)\s*(?:m|min|minutes?)");
            if (mMatch.Success && int.TryParse(mMatch.Groups[1].Value, out var m))
            {
                minutes = m;
            }

            if (days > 0 || hours > 0 || minutes > 0)
            {
                return DateTimeOffset.UtcNow.AddDays(days).AddHours(hours).AddMinutes(minutes);
            }
        }

        var absMatch = AbsoluteTimeResetRegex().Match(line);
        if (absMatch.Success)
        {
            var timeText = absMatch.Groups[1].Value;
            if (DateTime.TryParse(timeText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
            {
                return new DateTimeOffset(dt);
            }
        }

        return null;
    }
}
