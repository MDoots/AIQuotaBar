namespace AIQuotaBar.Core.Models;

public sealed record QuotaWindow
{
    public string Id { get; init; }
    public string DisplayName { get; init; }
    public int RawUsedPercent { get; init; }
    public int ClampedUsedPercent => Math.Clamp(RawUsedPercent, 0, 100);
    public int RemainingPercent => Math.Clamp(100 - RawUsedPercent, 0, 100);
    public TimeSpan? Duration { get; init; }
    public DateTimeOffset? ResetsAt { get; init; }
    public QuotaWindowStatus Status { get; init; }

    public QuotaWindow(
        string id,
        string displayName,
        int rawUsedPercent,
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
