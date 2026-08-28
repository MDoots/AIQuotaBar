namespace AIQuotaBar.App.Providers;

public interface IProviderDiscoveryService
{
    Task<IReadOnlyList<ProviderDiscoveryResult>> DiscoverAsync(
        IReadOnlyList<ProviderDescriptor> descriptors,
        CancellationToken cancellationToken = default);

    Task<ProviderDiscoveryResult> DiscoverSingleAsync(
        ProviderDescriptor descriptor,
        CancellationToken cancellationToken = default);
}
