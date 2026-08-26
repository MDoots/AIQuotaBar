namespace AIQuotaBar.Core.Utils;

public static class DurationFormatter
{
    public static string FormatWindowName(long? durationMinutes, string? defaultLabel = null)
    {
        if (!durationMinutes.HasValue || durationMinutes.Value <= 0)
        {
            return !string.IsNullOrWhiteSpace(defaultLabel) ? defaultLabel : "Quota Window";
        }

        var minutes = durationMinutes.Value;

        // Specific standard known durations
        if (minutes == 300) // 5 hours
        {
            return "5-Hour";
        }

        if (minutes == 10080) // 7 days (weekly)
        {
            return "Weekly";
        }

        if (minutes % 10080 == 0)
        {
            var weeks = minutes / 10080;
            return weeks == 1 ? "Weekly" : $"{weeks}-Week";
        }

        if (minutes % 1440 == 0)
        {
            var days = minutes / 1440;
            return days == 1 ? "Daily" : $"{days}-Day";
        }

        if (minutes % 60 == 0)
        {
            var hours = minutes / 60;
            return $"{hours}-Hour";
        }

        return $"{minutes}-Minute";
    }

    public static TimeSpan? ToTimeSpan(long? durationMinutes)
    {
        if (!durationMinutes.HasValue || durationMinutes.Value <= 0)
        {
            return null;
        }

        return TimeSpan.FromMinutes(durationMinutes.Value);
    }
}
