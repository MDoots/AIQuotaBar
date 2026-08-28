namespace AIQuotaBar.Providers.Codex;

using AIQuotaBar.Core.Interfaces;
using AIQuotaBar.Core.Models;
using AIQuotaBar.Providers.Codex.Normalization;
using AIQuotaBar.Providers.Codex.Protocol;
using AIQuotaBar.Providers.Codex.Transport;

public sealed class CodexUsageProvider : IUsageProvider
{
    public const string ProviderIdentifier = "codex";
    public const string ProviderName = "OpenAI Codex";

    private readonly ICodexProcessRunner _processRunner;
    private readonly Func<string?> _executableLocator;
    private readonly TimeSpan _defaultTimeout;

    public string Id => ProviderIdentifier;
    public string DisplayName => ProviderName;

    public CodexUsageProvider(
        ICodexProcessRunner? processRunner = null,
        Func<string?>? executableLocator = null,
        TimeSpan? defaultTimeout = null)
    {
        _processRunner = processRunner ?? new StandardCodexProcessRunner();
        _executableLocator = executableLocator ?? (() => CodexProcessLocator.LocateExecutable());
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
                statusMessage: "Codex executable not found on system");
        }

        CodexRateLimitsResult? rateLimitsResult = null;
        CodexAccountResult? accountResult = null;

        try
        {
            await _processRunner.RunAsync(
                executablePath,
                "app-server --stdio",
                async (session, runnerToken) =>
                {
                    var client = new CodexJsonRpcClient(session);

                    // 1. Send initialize and wait for initialize response + send initialized notification
                    await client.InitializeAsync("AIQuotaBar", "0.1.0", runnerToken).ConfigureAwait(false);

                    // 2. Query rate limits
                    rateLimitsResult = await client.SendRequestAsync<CodexRateLimitsResult>(
                        "account/rateLimits/read",
                        null,
                        runnerToken).ConfigureAwait(false);

                    // 3. Query account information (best-effort)
                    try
                    {
                        accountResult = await client.SendRequestAsync<CodexAccountResult>(
                            "account/read",
                            null,
                            runnerToken).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Best-effort enrichment; rateLimits is the primary source
                    }
                },
                _defaultTimeout,
                cancellationToken).ConfigureAwait(false);

            return CodexUsageNormalizer.Normalize(rateLimitsResult, accountResult);
        }
        catch (TimeoutException)
        {
            return new ProviderSnapshot(
                providerId: Id,
                providerDisplayName: DisplayName,
                status: ProviderStatus.Timeout,
                statusMessage: "Codex app-server did not respond");
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
                statusMessage: SafeErrorMessage(ex));
        }
    }

    private static bool IsAuthError(Exception ex)
    {
        return ex is CodexRpcException rpcEx &&
            (rpcEx.ErrorMessage?.Contains("auth", StringComparison.OrdinalIgnoreCase) == true ||
             rpcEx.ErrorMessage?.Contains("login", StringComparison.OrdinalIgnoreCase) == true ||
             rpcEx.ErrorMessage?.Contains("unauthenticated", StringComparison.OrdinalIgnoreCase) == true ||
             rpcEx.ErrorMessage?.Contains("not logged in", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static string SafeErrorMessage(Exception ex)
    {
        return ex switch
        {
            TimeoutException => "Codex app-server did not respond",
            EndOfStreamException => "Codex app-server closed connection unexpectedly",
            System.Text.Json.JsonException => "Codex returned an unexpected response",
            CodexRpcException rpcEx when rpcEx.ErrorCode == -32600 => "Codex rejected the request",
            CodexRpcException rpcEx when rpcEx.ErrorCode == -32601 => "Codex method not found",
            CodexRpcException rpcEx when IsAuthError(rpcEx) => "Codex is not authenticated",
            CodexRpcException => "Codex returned an unexpected response",
            _ => "Unable to communicate with Codex"
        };
    }
}
