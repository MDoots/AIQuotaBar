namespace AIQuotaBar.Core.Utils;

public static class CountdownFormatter
{
    public static string? FormatCountdown(DateTimeOffset? resetsAt, DateTimeOffset? referenceTime = null)
    {
        if (!resetsAt.HasValue)
        {
            return null;
        }

        var now = referenceTime ?? DateTimeOffset.UtcNow;
        var remaining = resetsAt.Value - now;

        if (remaining <= TimeSpan.Zero)
        {
            return "resets soon";
        }

        if (remaining.TotalDays >= 2)
        {
            var days = (int)Math.Floor(remaining.TotalDays);
            var hours = remaining.Hours;
            return hours > 0 ? $"resets in {days}d {hours}h" : $"resets in {days}d";
        }

        if (remaining.TotalHours >= 1)
        {
            var hours = (int)Math.Floor(remaining.TotalHours);
            var minutes = remaining.Minutes;
            return minutes > 0 ? $"resets in {hours}h {minutes}m" : $"resets in {hours}h";
        }

        if (remaining.TotalMinutes >= 1)
        {
            var minutes = (int)Math.Ceiling(remaining.TotalMinutes);
            return $"resets in {minutes}m";
        }

        return "resets in <1m";
    }
}
