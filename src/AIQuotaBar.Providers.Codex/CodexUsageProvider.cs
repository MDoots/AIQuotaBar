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
                async session =>
                {
                    var client = new CodexJsonRpcClient(session);

                    // 1. Send initialize and wait for initialize response + send initialized notification
                    await client.InitializeAsync("AIQuotaBar", "0.1.0", cancellationToken).ConfigureAwait(false);

                    // 2. Query rate limits
                    rateLimitsResult = await client.SendRequestAsync<CodexRateLimitsResult>(
                        "account/rateLimits/read",
                        null,
                        cancellationToken).ConfigureAwait(false);

                    // 3. Query account information (best-effort)
                    try
                    {
                        accountResult = await client.SendRequestAsync<CodexAccountResult>(
                            "account/read",
                            null,
                            cancellationToken).ConfigureAwait(false);
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
        catch (TimeoutException ex)
        {
            return new ProviderSnapshot(
                providerId: Id,
                providerDisplayName: DisplayName,
                status: ProviderStatus.Timeout,
                statusMessage: ex.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ProviderSnapshot(
                providerId: Id,
                providerDisplayName: DisplayName,
                status: ProviderStatus.Cancelled,
                statusMessage: "Refresh cancelled by user");
        }
        catch (CodexRpcException ex)
        {
            return new ProviderSnapshot(
                providerId: Id,
                providerDisplayName: DisplayName,
                status: ProviderStatus.Error,
                statusMessage: $"Codex RPC error: {SanitizeErrorMessage(ex.ErrorMessage ?? ex.Message)}");
        }
        catch (Exception ex)
        {
            return new ProviderSnapshot(
                providerId: Id,
                providerDisplayName: DisplayName,
                status: ProviderStatus.Error,
                statusMessage: $"Error communicating with Codex: {SanitizeErrorMessage(ex.Message)}");
        }
    }

    private static string SanitizeErrorMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Unknown error";
        }

        // Avoid exposing any accidental paths or tokens in UI
        if (message.Length > 120)
        {
            return message[..120] + "...";
        }

        return message;
    }
}
