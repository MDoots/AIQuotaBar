namespace AIQuotaBar.Providers.GitHubCopilot;

using AIQuotaBar.Core.Interfaces;
using AIQuotaBar.Core.Models;
using AIQuotaBar.Providers.GitHubCopilot.Adapter;
using AIQuotaBar.Providers.GitHubCopilot.Normalization;

public sealed class GitHubCopilotUsageProvider : IUsageProvider
{
    public const string ProviderIdentifier = "github-copilot";
    public const string ProviderName = "GitHub Copilot";

    private readonly ICopilotClientAdapter _adapter;
    private readonly Func<string?> _executableLocator;
    private readonly TimeSpan _defaultTimeout;

    public string Id => ProviderIdentifier;
    public string DisplayName => ProviderName;

    public GitHubCopilotUsageProvider(
        ICopilotClientAdapter? adapter = null,
        Func<string?>? executableLocator = null,
        TimeSpan? defaultTimeout = null)
    {
        _adapter = adapter ?? new StandardCopilotClientAdapter();
        _executableLocator = executableLocator ?? (() => GitHubCopilotProcessLocator.LocateExecutable());
        _defaultTimeout = defaultTimeout ?? TimeSpan.FromSeconds(8);
    }

    public async Task<ProviderSnapshot> GetUsageAsync(CancellationToken cancellationToken = default)
    {
        var executablePath = _executableLocator();
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return new ProviderSnapshot(
                providerId: Id,
                providerDisplayName: DisplayName,
                status: ProviderStatus.Unavailable,
                statusMessage: "GitHub Copilot executable not found on system");
        }

        try
        {
            var fetchResult = await _adapter.FetchQuotasAsync(
                executablePath,
                _defaultTimeout,
                cancellationToken).ConfigureAwait(false);

            return CopilotUsageNormalizer.Normalize(fetchResult);
        }
        catch (TimeoutException)
        {
            return new ProviderSnapshot(
                providerId: Id,
                providerDisplayName: DisplayName,
                status: ProviderStatus.Timeout,
                statusMessage: "GitHub Copilot did not respond");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ProviderSnapshot(
                providerId: Id,
                providerDisplayName: DisplayName,
                status: ProviderStatus.Cancelled,
                statusMessage: "Refresh cancelled by user");
        }
        catch (Exception)
        {
            return new ProviderSnapshot(
                providerId: Id,
                providerDisplayName: DisplayName,
                status: ProviderStatus.Error,
                statusMessage: "Unable to communicate with GitHub Copilot");
        }
    }
}
