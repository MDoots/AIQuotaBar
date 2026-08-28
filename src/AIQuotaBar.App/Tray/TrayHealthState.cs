namespace AIQuotaBar.App.Tray;

using AIQuotaBar.App.Health;

public sealed record TrayHealthState(
    QuotaHealthLevel HealthLevel,
    double? LowestRemainingPercent,
    string? ProviderName,
    string? WindowName,
    bool HasVisibleQuotaData,
    bool HasVisibleProviders,
    string TooltipText,
    string StatusMenuText);
