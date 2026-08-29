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

    [GeneratedRegex(@"(\d+(?:\.\d+)?)\s*%\s*(?:remaining|left)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex RemainingPercentRegex();

    [GeneratedRegex(@"(?:remaining|left)\s*[:=]\s*(\d+(?:\.\d+)?)\s*%", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex RemainingColonPercentRegex();

    [GeneratedRegex(@"resets?\s+(?:in\s+)?([0-9]+\s*(?:h|hr|hours?|m|min|minutes?|d|days?)(?:\s+[0-9]+\s*(?:m|min|minutes?))?)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex RelativeResetRegex();

    [GeneratedRegex(@"resets?\s+(?:at\s+)?([0-9]{1,2}:[0-9]{2}(?:\s*[ap]m)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex AbsoluteTimeResetRegex();

    public static ProviderSnapshot Normalize(string? rawOutput, string? plan = null, DateTimeOffset? now = null)
    {
        var currentWallClock = now ?? DateTimeOffset.UtcNow;

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
            var rawUsedPercent = ParseExplicitUsedPercent(line);
            if (!rawUsedPercent.HasValue)
            {
                continue;
            }

            var usedVal = rawUsedPercent.Value;
            var lower = line.ToLowerInvariant();

            string? windowId = null;
            string? displayName = null;
            TimeSpan? duration = null;

            if (lower.Contains("opus"))
            {
                windowId = "weekly-opus";
                displayName = "Weekly · Claude Opus";
                duration = TimeSpan.FromDays(7);
            }
            else if (lower.Contains("session") || lower.Contains("5-hour") || lower.Contains("5h") || lower.Contains("hourly"))
            {
                windowId = "session-5h";
                displayName = "Session · 5-hour";
                duration = TimeSpan.FromHours(5);
            }
            else if ((lower.Contains("weekly") || lower.Contains("week") || lower.Contains("7-day") || lower.Contains("all models")) &&
                     !lower.Contains("sonnet") && !lower.Contains("haiku"))
            {
                windowId = "weekly-all";
                displayName = "Weekly · All models";
                duration = TimeSpan.FromDays(7);
            }

            // If section is unrecognized (e.g. unknown model pool or bare unmapped line): ignore safely, never invent unknown windows
            if (windowId == null || displayName == null)
            {
                continue;
            }

            // Deduplicate if multiple lines match the same window
            if (windows.Any(w => w.Id == windowId))
            {
                continue;
            }

            var resetsAt = ParseResetTime(line, currentWallClock);
            var status = usedVal >= 100.0 ? QuotaWindowStatus.Exhausted : QuotaWindowStatus.Active;

            windows.Add(new QuotaWindow(
                id: windowId,
                displayName: displayName,
                rawUsedPercent: usedVal,
                duration: duration,
                resetsAt: resetsAt,
                status: status));
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

    private static double? ParseExplicitUsedPercent(string line)
    {
        var usedMatch = UsedPercentRegex().Match(line);
        if (!usedMatch.Success)
        {
            usedMatch = UsedColonPercentRegex().Match(line);
        }

        if (usedMatch.Success && double.TryParse(usedMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var uVal))
        {
            return Math.Clamp(uVal, 0.0, 100.0);
        }

        var remMatch = RemainingPercentRegex().Match(line);
        if (!remMatch.Success)
        {
            remMatch = RemainingColonPercentRegex().Match(line);
        }

        if (remMatch.Success && double.TryParse(remMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var rVal))
        {
            var clampedRem = Math.Clamp(rVal, 0.0, 100.0);
            return Math.Clamp(100.0 - clampedRem, 0.0, 100.0);
        }

        return null;
    }

    private static DateTimeOffset? ParseResetTime(string line, DateTimeOffset now)
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
                return now.AddDays(days).AddHours(hours).AddMinutes(minutes);
            }
        }

        var absMatch = AbsoluteTimeResetRegex().Match(line);
        if (absMatch.Success)
        {
            var timeText = absMatch.Groups[1].Value;
            if (TimeOnly.TryParse(timeText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timeOnly))
            {
                var candidate = new DateTimeOffset(now.Year, now.Month, now.Day, timeOnly.Hour, timeOnly.Minute, 0, now.Offset);
                if (candidate <= now)
                {
                    candidate = candidate.AddDays(1);
                }
                return candidate;
            }

            if (DateTime.TryParse(timeText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
            {
                var candidate = new DateTimeOffset(dt);
                if (candidate <= now)
                {
                    candidate = candidate.AddDays(1);
                }
                return candidate;
            }
        }

        return null;
    }
}
