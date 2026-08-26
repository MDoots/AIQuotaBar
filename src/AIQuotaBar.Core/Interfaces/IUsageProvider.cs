namespace AIQuotaBar.Core.Interfaces;

using AIQuotaBar.Core.Models;

public interface IUsageProvider
{
    string Id { get; }
    string DisplayName { get; }

    Task<ProviderSnapshot> GetUsageAsync(CancellationToken cancellationToken = default);
}
