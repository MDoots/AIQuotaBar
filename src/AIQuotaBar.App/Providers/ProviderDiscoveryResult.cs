namespace AIQuotaBar.App.Providers;

public sealed record ProviderDiscoveryResult(
    string ProviderId,
    ProviderDiscoveryStatus Status,
    string? DetectedExecutablePath = null,
    string? SafeMessage = null);
