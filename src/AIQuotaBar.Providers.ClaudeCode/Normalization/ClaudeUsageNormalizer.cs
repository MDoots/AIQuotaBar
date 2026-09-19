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

    [GeneratedRegex(@"resets?\s+(?:in\s+)?((?:[0-9]+\s*(?:weeks?|days?|hours?|minutes?|seconds?|hr|min|sec|w|d|h|m|s)(?=\s|$|\d|[),])\s*)+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex RelativeResetRegex();

    [GeneratedRegex(@"resets?\s+(?:at\s+)?([0-9]{1,2}:[0-9]{2}(?:\s*[ap]m)?)(?:\s+\(?((?:UTC|GMT)?[+-][0-9]{2}:?[0-9]{2}|[A-Za-z_]+(?:/[A-Za-z_]+)*))?", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex AbsoluteTimeResetRegex();

    public static ProviderSnapshot Normalize(
        string? rawOutput,
        string? plan = null,
        DateTimeOffset? now = null,
        TimeZoneInfo? timeZone = null)
    {
        // An explicit instant is commonly supplied by deterministic callers. If
        // no zone is supplied, preserve that instant's offset; live calls use the
        // machine's local wall clock.
        var zone = timeZone ?? (now.HasValue
            ? TimeZoneInfo.CreateCustomTimeZone("ExplicitOffset", now.Value.Offset, "Explicit offset", "Explicit offset")
            : TimeZoneInfo.Local);
        var currentWallClock = now.HasValue
            ? TimeZoneInfo.ConvertTime(now.Value, zone)
            : TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);

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

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
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

            // Some Claude versions put the reset on the following line. Associate
            // only adjacent reset text with this quota row.
            var resetContext = line;
            if (!HasResetText(resetContext) && lineIndex + 1 < lines.Length && HasResetText(lines[lineIndex + 1]))
            {
                var nextLineHasQuota = ParseExplicitUsedPercent(lines[lineIndex + 1]).HasValue;
                if (!nextLineHasQuota)
                {
                    resetContext = lines[lineIndex + 1];
                }
            }

            var resetsAt = ParseResetTime(resetContext, currentWallClock, zone);
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

    private static bool HasResetText(string line) => line.Contains("reset", StringComparison.OrdinalIgnoreCase);

    private static DateTimeOffset? ParseResetTime(string line, DateTimeOffset now, TimeZoneInfo zone)
    {
        var relMatch = RelativeResetRegex().Match(line);
        if (relMatch.Success)
        {
            var relText = relMatch.Groups[1].Value.ToLowerInvariant();
            var duration = TimeSpan.Zero;
            foreach (Match component in Regex.Matches(relText, @"(\d+)\s*(weeks?|days?|hours?|minutes?|seconds?|hr|min|sec|w|d|h|m|s)(?=\s|$|\d|[),])", RegexOptions.IgnoreCase))
            {
                if (!long.TryParse(component.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                {
                    return null;
                }

                var unit = component.Groups[2].Value.ToLowerInvariant();
                try
                {
                    duration += unit.StartsWith("w", StringComparison.Ordinal) ? TimeSpan.FromDays(checked(value * 7)) :
                        unit.StartsWith("d", StringComparison.Ordinal) ? TimeSpan.FromDays(value) :
                        unit.StartsWith("h", StringComparison.Ordinal) ? TimeSpan.FromHours(value) :
                        unit.StartsWith("s", StringComparison.Ordinal) ? TimeSpan.FromSeconds(value) :
                        TimeSpan.FromMinutes(value);
                }
                catch (OverflowException)
                {
                    return null;
                }
            }

            if (duration > TimeSpan.Zero)
                try
                {
                    return now.Add(duration);
                }
                catch (ArgumentOutOfRangeException)
                {
                    return null;
                }
        }

        var absMatch = AbsoluteTimeResetRegex().Match(line);
        if (absMatch.Success)
        {
            var timeText = absMatch.Groups[1].Value;
            var suffix = absMatch.Groups[2].Value;
            if (suffix.Length > 3 && (suffix.StartsWith("UTC", StringComparison.OrdinalIgnoreCase) || suffix.StartsWith("GMT", StringComparison.OrdinalIgnoreCase)))
                suffix = suffix[3..];
            var effectiveZone = zone;
            if (!string.IsNullOrWhiteSpace(suffix))
            {
                if (suffix.Equals("UTC", StringComparison.OrdinalIgnoreCase) || suffix.Equals("GMT", StringComparison.OrdinalIgnoreCase))
                {
                    effectiveZone = TimeZoneInfo.Utc;
                }
                else if (suffix.StartsWith("+", StringComparison.Ordinal) || suffix.StartsWith("-", StringComparison.Ordinal))
                {
                    var sign = suffix[0] == '-' ? -1 : 1;
                    var offsetText = suffix[1..].Replace(":", string.Empty, StringComparison.Ordinal);
                    if (!int.TryParse(offsetText[..2], NumberStyles.None, CultureInfo.InvariantCulture, out var offsetHours) ||
                        !int.TryParse(offsetText[2..], NumberStyles.None, CultureInfo.InvariantCulture, out var offsetMinutes) ||
                        offsetHours > 14 || offsetMinutes > 59 || (offsetHours == 14 && offsetMinutes != 0))
                    {
                        return null;
                    }
                    var offset = new TimeSpan(sign * offsetHours, sign * offsetMinutes, 0);
                    effectiveZone = TimeZoneInfo.CreateCustomTimeZone("OutputOffset", offset, "Output offset", "Output offset");
                }
                else
                {
                    try { effectiveZone = TimeZoneInfo.FindSystemTimeZoneById(suffix); }
                    catch (TimeZoneNotFoundException) { return null; }
                    catch (InvalidTimeZoneException) { return null; }
                }
            }
            if (TimeOnly.TryParse(timeText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timeOnly))
            {
                var effectiveNow = TimeZoneInfo.ConvertTime(now, effectiveZone);
                var localDateTime = new DateTime(effectiveNow.Year, effectiveNow.Month, effectiveNow.Day, timeOnly.Hour, timeOnly.Minute, 0, DateTimeKind.Unspecified);
                if (effectiveZone.IsInvalidTime(localDateTime) || effectiveZone.IsAmbiguousTime(localDateTime))
                {
                    return null;
                }

                var candidate = new DateTimeOffset(localDateTime, effectiveZone.GetUtcOffset(localDateTime));
                if (candidate <= effectiveNow)
                {
                    localDateTime = localDateTime.AddDays(1);
                    if (effectiveZone.IsInvalidTime(localDateTime) || effectiveZone.IsAmbiguousTime(localDateTime))
                    {
                        return null;
                    }
                    candidate = new DateTimeOffset(localDateTime, effectiveZone.GetUtcOffset(localDateTime));
                }
                return candidate;
            }
        }

        return null;
    }
}
