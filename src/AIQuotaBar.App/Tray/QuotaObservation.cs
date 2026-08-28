namespace AIQuotaBar.App.Tray;

using AIQuotaBar.Core.Models;

public sealed record QuotaObservation(
    string ProviderId,
    string ProviderDisplayName,
    string WindowId,
    string WindowDisplayName,
    double RemainingPercent,
    QuotaWindowStatus Status = QuotaWindowStatus.Active);
