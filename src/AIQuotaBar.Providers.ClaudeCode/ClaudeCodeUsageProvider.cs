namespace AIQuotaBar.Providers.ClaudeCode;

using AIQuotaBar.Core.Interfaces;
using AIQuotaBar.Core.Models;
using AIQuotaBar.Providers.ClaudeCode.Normalization;
using AIQuotaBar.Providers.ClaudeCode.Transport;

public sealed class ClaudeCodeUsageProvider : IUsageProvider
{
    public const string ProviderIdentifier = "claude-code";
    public const string ProviderName = "Claude Code";

    private readonly IClaudeProcessRunner _runner;
    private readonly Func<string?> _executableLocator;
    private readonly TimeSpan _defaultTimeout;

    public string Id => ProviderIdentifier;
    public string DisplayName => ProviderName;

    public ClaudeCodeUsageProvider(
        IClaudeProcessRunner? runner = null,
        Func<string?>? executableLocator = null,
        TimeSpan? defaultTimeout = null)
    {
        _runner = runner ?? new StandardClaudeProcessRunner();
        _executableLocator = executableLocator ?? (() => ClaudeCodeProcessLocator.LocateExecutable());
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
                statusMessage: "Claude Code executable not found on system");
        }

        // Single overall bounded time budget for auth check + usage capture + cleanup
        using var overallCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        overallCts.CancelAfter(_defaultTimeout);

        try
        {
            var authStatus = await _runner.CheckAuthStatusAsync(
                executablePath,
                _defaultTimeout,
                overallCts.Token).ConfigureAwait(false);

            if (authStatus == null)
            {
                // Unknown/malformed auth status: fail closed, do NOT launch interactive session
                return new ProviderSnapshot(
                    providerId: Id,
                    providerDisplayName: DisplayName,
                    status: ProviderStatus.Unavailable,
                    statusMessage: "Unable to determine Claude Code authentication status");
            }

            if (!authStatus.LoggedIn)
            {
                return new ProviderSnapshot(
                    providerId: Id,
                    providerDisplayName: DisplayName,
                    status: ProviderStatus.Unauthenticated,
                    statusMessage: "Claude Code requires sign-in",
                    accountPlan: authStatus.SubscriptionTier);
            }

            // Execute usage capture only when authenticated
            var usageOutput = await _runner.CaptureUsageAsync(
                executablePath,
                _defaultTimeout,
                overallCts.Token).ConfigureAwait(false);

            return ClaudeUsageNormalizer.Normalize(usageOutput, authStatus.SubscriptionTier);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ProviderSnapshot(
                providerId: Id,
                providerDisplayName: DisplayName,
                status: ProviderStatus.Cancelled,
                statusMessage: "Refresh cancelled by user");
        }
        catch (OperationCanceledException) when (overallCts.IsCancellationRequested)
        {
            return new ProviderSnapshot(
                providerId: Id,
                providerDisplayName: DisplayName,
                status: ProviderStatus.Timeout,
                statusMessage: "Claude Code did not respond");
        }
        catch (TimeoutException)
        {
            return new ProviderSnapshot(
                providerId: Id,
                providerDisplayName: DisplayName,
                status: ProviderStatus.Timeout,
                statusMessage: "Claude Code did not respond");
        }
        catch (Exception)
        {
            return new ProviderSnapshot(
                providerId: Id,
                providerDisplayName: DisplayName,
                status: ProviderStatus.Error,
                statusMessage: "Unable to communicate with Claude Code");
        }
    }
}
