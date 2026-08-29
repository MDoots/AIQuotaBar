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

    private static readonly HashSet<string> KnownSafeAuthMethodIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "cached_token",
        "token",
        "api_key",
        "apikey"
    };

    private static readonly HashSet<string> KnownSafeAuthMethodTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "cached_token",
        "token",
        "api_key",
        "apikey"
    };

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
        if (cancellationToken.IsCancellationRequested)
        {
            return new ProviderSnapshot(
                providerId: Id,
                providerDisplayName: DisplayName,
                status: ProviderStatus.Cancelled,
                statusMessage: "Refresh cancelled by user");
        }

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

                    var initResult = await client.InitializeAsync("AIQuotaBar", "1.0.0", runnerToken).ConfigureAwait(false);
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
            // Fail closed on null or unparseable initialize response
            return null;
        }

        var authMethods = initResult.AuthMethods;
        if (authMethods == null || authMethods.Count == 0)
        {
            // Legacy compatibility: If a valid initialize response was received with server/protocol metadata
            // but no authMethods array was advertised by the legacy version, allow legacy cached_token fallback.
            var isValidLegacyInit = initResult.ProtocolVersion.HasValue ||
                                   initResult.ServerInfo != null ||
                                   initResult.Capabilities != null;

            return isValidLegacyInit ? "cached_token" : null;
        }

        // Strict whitelist: only select explicitly understood non-interactive provider-owned methods
        var nonInteractiveSafe = authMethods
            .Where(IsWhitelistedNonInteractiveMethod)
            .ToList();

        if (nonInteractiveSafe.Count == 0)
        {
            // No understood safe non-interactive auth method offered -> fail closed
            return null;
        }

        // If defaultAuthMethodId is in the whitelisted safe collection, prefer it
        if (!string.IsNullOrWhiteSpace(initResult.Meta?.DefaultAuthMethodId))
        {
            var defaultMatch = nonInteractiveSafe.FirstOrDefault(m => string.Equals(m.Id, initResult.Meta.DefaultAuthMethodId, StringComparison.OrdinalIgnoreCase));
            if (defaultMatch != null)
            {
                return defaultMatch.Id;
            }
        }

        // Prefer cached_token
        var cachedToken = nonInteractiveSafe.FirstOrDefault(m => string.Equals(m.Id, "cached_token", StringComparison.OrdinalIgnoreCase));
        if (cachedToken != null)
        {
            return cachedToken.Id;
        }

        // Otherwise return first whitelisted safe method (e.g. token/apiKey)
        return nonInteractiveSafe[0].Id;
    }

    private static bool IsWhitelistedNonInteractiveMethod(GrokAuthMethod method)
    {
        if (method.Interactive == true)
        {
            return false;
        }

        var id = method.Id ?? string.Empty;
        var type = method.Type ?? string.Empty;

        if (string.Equals(type, "oauth", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, "browser", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("browser", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("oauth", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("interactive", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return KnownSafeAuthMethodIds.Contains(id) || KnownSafeAuthMethodTypes.Contains(type);
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
