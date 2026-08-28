namespace AIQuotaBar.App.Providers;

public sealed class ProviderDiscoveryService : IProviderDiscoveryService
{
    public async Task<IReadOnlyList<ProviderDiscoveryResult>> DiscoverAsync(
        IReadOnlyList<ProviderDescriptor> descriptors,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        var tasks = descriptors.Select(d => DiscoverSingleAsync(d, cancellationToken));
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results;
    }

    public async Task<ProviderDiscoveryResult> DiscoverSingleAsync(
        ProviderDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var path = descriptor.LocateExecutable();
                if (!string.IsNullOrWhiteSpace(path))
                {
                    return new ProviderDiscoveryResult(
                        ProviderId: descriptor.Id,
                        Status: ProviderDiscoveryStatus.Detected,
                        DetectedExecutablePath: path);
                }

                return new ProviderDiscoveryResult(
                    ProviderId: descriptor.Id,
                    Status: ProviderDiscoveryStatus.NotDetected);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                return new ProviderDiscoveryResult(
                    ProviderId: descriptor.Id,
                    Status: ProviderDiscoveryStatus.Error,
                    SafeMessage: $"Unable to check {descriptor.DisplayName} installation.");
            }
        }, cancellationToken).ConfigureAwait(false);
    }
}
