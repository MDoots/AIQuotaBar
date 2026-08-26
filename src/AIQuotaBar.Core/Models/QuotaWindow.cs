namespace AIQuotaBar.Core.Models;

public sealed record QuotaWindow
{
    public string Id { get; init; }
    public string DisplayName { get; init; }
    public double RawUsedPercent { get; init; }
    public double ClampedUsedPercent => Math.Clamp(RawUsedPercent, 0.0, 100.0);
    public double RemainingPercent => Math.Clamp(100.0 - RawUsedPercent, 0.0, 100.0);
    public TimeSpan? Duration { get; init; }
    public DateTimeOffset? ResetsAt { get; init; }
    public QuotaWindowStatus Status { get; init; }

    public QuotaWindow(
        string id,
        string displayName,
        double rawUsedPercent,
        TimeSpan? duration,
        DateTimeOffset? resetsAt,
        QuotaWindowStatus status = QuotaWindowStatus.Active)
    {
        Id = id ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        RawUsedPercent = rawUsedPercent;
        Duration = duration;
        ResetsAt = resetsAt;
        Status = status;
    }
}
