namespace AIQuotaBar.Providers.GrokBuild;

using AIQuotaBar.Core.Interfaces;
using AIQuotaBar.Core.Models;
using AIQuotaBar.Providers.GrokBuild.Normalization;
using AIQuotaBar.Providers.GrokBuild.Protocol;
using AIQuotaBar.Providers.GrokBuild.Transport;

public sealed class GrokBuildUsageProvider : IUsageProvider
{
    public const string ProviderIdentifier = "grok-build";
    public const string ProviderName = "Grok Build";

    private readonly IGrokProcessRunner _processRunner;
    private readonly Func<string?> _executableLocator;
    private readonly TimeSpan _defaultTimeout;

    public string Id => ProviderIdentifier;
    public string DisplayName => ProviderName;

    public GrokBuildUsageProvider(
        IGrokProcessRunner? processRunner = null,
        Func<string?>? executableLocator = null,
        TimeSpan? defaultTimeout = null)
    {
        _processRunner = processRunner ?? new StandardGrokProcessRunner();
        _executableLocator = executableLocator ?? (() => GrokBuildProcessLocator.LocateExecutable());
        _defaultTimeout = defaultTimeout ?? TimeSpan.FromSeconds(6);
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
                statusMessage: "Grok Build executable not found on system");
        }

        GrokBillingResult? billingResult = null;

        try
        {
            await _processRunner.RunAsync(
                executablePath,
                "--no-auto-update agent stdio",
                async (session, runnerToken) =>
                {
                    var client = new GrokJsonRpcClient(session);

                    await client.InitializeAsync("AIQuotaBar", "0.2.0", runnerToken).ConfigureAwait(false);
                    await client.AuthenticateAsync("cached_token", runnerToken).ConfigureAwait(false);
                    billingResult = await client.GetBillingAsync(runnerToken).ConfigureAwait(false);
                },
                _defaultTimeout,
                cancellationToken).ConfigureAwait(false);

            return GrokUsageNormalizer.Normalize(billingResult);
        }
        catch (GrokAuthException)
        {
            return new ProviderSnapshot(
                providerId: Id,
                providerDisplayName: DisplayName,
                status: ProviderStatus.Unauthenticated,
                statusMessage: "Grok Build requires sign-in");
        }
        catch (TimeoutException)
        {
            return new ProviderSnapshot(
                providerId: Id,
                providerDisplayName: DisplayName,
                status: ProviderStatus.Timeout,
                statusMessage: "Grok Build did not respond");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ProviderSnapshot(
                providerId: Id,
                providerDisplayName: DisplayName,
                status: ProviderStatus.Cancelled,
                statusMessage: "Refresh cancelled by user");
        }
        catch (Exception ex)
        {
            var isAuth = IsAuthError(ex);
            return new ProviderSnapshot(
                providerId: Id,
                providerDisplayName: DisplayName,
                status: isAuth ? ProviderStatus.Unauthenticated : ProviderStatus.Error,
                statusMessage: isAuth ? "Grok Build requires sign-in" : "Unable to communicate with Grok Build");
        }
    }

    private static bool IsAuthError(Exception ex)
    {
        return ex is GrokAuthException ||
            (ex is GrokRpcException rpcEx &&
             (rpcEx.ErrorMessage?.Contains("auth", StringComparison.OrdinalIgnoreCase) == true ||
              rpcEx.ErrorMessage?.Contains("login", StringComparison.OrdinalIgnoreCase) == true ||
              rpcEx.ErrorMessage?.Contains("token", StringComparison.OrdinalIgnoreCase) == true));
    }
}
