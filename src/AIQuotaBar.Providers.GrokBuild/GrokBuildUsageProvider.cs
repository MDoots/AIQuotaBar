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

    private static readonly string[] DefaultArguments = new[] { "--no-auto-update", "agent", "stdio" };

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
                DefaultArguments,
                async (session, runnerToken) =>
                {
                    var client = new GrokJsonRpcClient(session);

                    var initResult = await client.InitializeAsync("AIQuotaBar", "0.2.0", runnerToken).ConfigureAwait(false);
                    var authMethodId = SelectNonInteractiveAuthMethod(initResult);

                    if (string.IsNullOrWhiteSpace(authMethodId))
                    {
                        throw new GrokAuthException("No non-interactive Grok Build authentication method available");
                    }

                    await client.AuthenticateAsync(authMethodId, runnerToken).ConfigureAwait(false);
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

    public static string? SelectNonInteractiveAuthMethod(GrokInitializeResult? initResult)
    {
        if (initResult == null)
        {
            return "cached_token";
        }

        var authMethods = initResult.AuthMethods;
        if (authMethods == null || authMethods.Count == 0)
        {
            // If no authMethods array is advertised by legacy version, use default provider-owned cached token
            return initResult.Meta?.DefaultAuthMethodId ?? "cached_token";
        }

        // Filter out explicitly interactive methods (e.g. browser login)
        var nonInteractive = authMethods
            .Where(m => !IsInteractive(m))
            .ToList();

        if (nonInteractive.Count == 0)
        {
            // All advertised methods are interactive -> fail closed
            return null;
        }

        // If defaultAuthMethodId is among the non-interactive methods, prefer it
        if (!string.IsNullOrWhiteSpace(initResult.Meta?.DefaultAuthMethodId))
        {
            var defaultMatch = nonInteractive.FirstOrDefault(m => string.Equals(m.Id, initResult.Meta.DefaultAuthMethodId, StringComparison.OrdinalIgnoreCase));
            if (defaultMatch != null)
            {
                return defaultMatch.Id;
            }
        }

        // Prefer cached_token or token/apiKey
        var cachedToken = nonInteractive.FirstOrDefault(m => string.Equals(m.Id, "cached_token", StringComparison.OrdinalIgnoreCase));
        if (cachedToken != null)
        {
            return cachedToken.Id;
        }

        var tokenMethod = nonInteractive.FirstOrDefault(m => string.Equals(m.Type, "token", StringComparison.OrdinalIgnoreCase) || string.Equals(m.Type, "apiKey", StringComparison.OrdinalIgnoreCase));
        if (tokenMethod != null)
        {
            return tokenMethod.Id;
        }

        return nonInteractive[0].Id;
    }

    private static bool IsInteractive(GrokAuthMethod method)
    {
        if (method.Interactive == true)
        {
            return true;
        }

        if (string.Equals(method.Type, "oauth", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(method.Type, "browser", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (method.Id.Contains("browser", StringComparison.OrdinalIgnoreCase) ||
            method.Id.Contains("oauth", StringComparison.OrdinalIgnoreCase) ||
            method.Id.Contains("interactive", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
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
