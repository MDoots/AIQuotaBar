namespace AIQuotaBar.Providers.ClaudeCode.Transport;

using System.Text.Json.Serialization;

public sealed class ClaudeAuthStatusResult
{
    [JsonPropertyName("loggedIn")]
    public bool LoggedIn { get; set; }

    [JsonPropertyName("authMethod")]
    public string? AuthMethod { get; set; }

    [JsonPropertyName("apiProvider")]
    public string? ApiProvider { get; set; }

    [JsonPropertyName("subscriptionTier")]
    public string? SubscriptionTier { get; set; }
}

public interface IClaudeProcessRunner
{
    Task<ClaudeAuthStatusResult?> CheckAuthStatusAsync(
        string executablePath,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    Task<string> CaptureUsageAsync(
        string executablePath,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
