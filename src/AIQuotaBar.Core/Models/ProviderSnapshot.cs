namespace AIQuotaBar.Core.Models;

public sealed record ProviderSnapshot
{
    public string ProviderId { get; init; }
    public string ProviderDisplayName { get; init; }
    public ProviderStatus Status { get; init; }
    public string? StatusMessage { get; init; }
    public string? AccountPlan { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public IReadOnlyList<QuotaWindow> Windows { get; init; }

    public ProviderSnapshot(
        string providerId,
        string providerDisplayName,
        ProviderStatus status,
        string? statusMessage = null,
        string? accountPlan = null,
        DateTimeOffset? timestamp = null,
        IReadOnlyList<QuotaWindow>? windows = null)
    {
        ProviderId = providerId ?? string.Empty;
        ProviderDisplayName = providerDisplayName ?? string.Empty;
        Status = status;
        StatusMessage = statusMessage;
        AccountPlan = accountPlan;
        Timestamp = timestamp ?? DateTimeOffset.UtcNow;
        Windows = windows ?? Array.Empty<QuotaWindow>();
    }
}
