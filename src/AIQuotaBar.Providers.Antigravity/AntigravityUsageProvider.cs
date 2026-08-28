namespace AIQuotaBar.Providers.Antigravity;

using System.Text.Json;
using AIQuotaBar.Core.Interfaces;
using AIQuotaBar.Core.Models;
using AIQuotaBar.Providers.Antigravity.Normalization;
using AIQuotaBar.Providers.Antigravity.Protocol;
using AIQuotaBar.Providers.Antigravity.Transport;

public sealed class AntigravityUsageProvider : IUsageProvider
{
    public const string ProviderIdentifier = "antigravity";
    public const string ProviderName = "Google Antigravity";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly string[] DefaultArguments = ["-p", "/usage", "--output-format", "json"];

    private readonly IAntigravityProcessRunner _processRunner;
    private readonly Func<string?> _executableLocator;
    private readonly TimeSpan _defaultTimeout;

    public string Id => ProviderIdentifier;
    public string DisplayName => ProviderName;

    public AntigravityUsageProvider(
        IAntigravityProcessRunner? processRunner = null,
        Func<string?>? executableLocator = null,
        TimeSpan? defaultTimeout = null)
    {
        _processRunner = processRunner ?? new StandardAntigravityProcessRunner();
        _executableLocator = executableLocator ?? (() => AntigravityProcessLocator.LocateExecutable());
        _defaultTimeout = defaultTimeout ?? TimeSpan.FromSeconds(10);
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
                statusMessage: "Antigravity CLI not found on system");
        }

        try
        {
            var rawOutput = await _processRunner.RunAsync(
                executablePath,
                DefaultArguments,
                _defaultTimeout,
                cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(rawOutput))
            {
                return new ProviderSnapshot(
                    providerId: Id,
                    providerDisplayName: DisplayName,
                    status: ProviderStatus.Unavailable,
                    statusMessage: "No response from Antigravity CLI");
            }

            AntigravityCliResponse? response = null;
            try
            {
                response = JsonSerializer.Deserialize<AntigravityCliResponse>(rawOutput.Trim(), JsonOptions);
            }
            catch (JsonException)
            {
                return new ProviderSnapshot(
                    providerId: Id,
                    providerDisplayName: DisplayName,
                    status: ProviderStatus.Error,
                    statusMessage: "Antigravity returned an unexpected response format");
            }

            return AntigravityUsageNormalizer.Normalize(response);
        }
        catch (TimeoutException)
        {
            return new ProviderSnapshot(
                providerId: Id,
                providerDisplayName: DisplayName,
                status: ProviderStatus.Timeout,
                statusMessage: "Antigravity CLI query timed out");
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
        var msg = ex.Message ?? string.Empty;
        return msg.Contains("auth", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("login", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("unauthenticated", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("not logged in", StringComparison.OrdinalIgnoreCase);
    }

    private static string SafeErrorMessage(Exception ex)
    {
        if (IsAuthError(ex))
        {
            return "Antigravity CLI requires authentication";
        }

        return "Unable to communicate with Antigravity CLI";
    }
}
