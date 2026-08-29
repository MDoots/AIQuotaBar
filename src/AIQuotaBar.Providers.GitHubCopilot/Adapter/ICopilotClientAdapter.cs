namespace AIQuotaBar.Providers.GitHubCopilot.Adapter;

public sealed class CopilotAuthInfoDto
{
    public bool IsAuthenticated { get; set; }
    public string? Login { get; set; }
    public string? Plan { get; set; }
    public string? AccessTypeSku { get; set; }
    public string? StatusMessage { get; set; }
}

public sealed class CopilotQuotaDto
{
    public string Key { get; set; } = string.Empty;
    public long EntitlementRequests { get; set; }
    public bool IsUnlimitedEntitlement { get; set; }
    public long UsedRequests { get; set; }
    public double RemainingPercentage { get; set; }
    public DateTimeOffset? ResetDate { get; set; }
    public double Overage { get; set; }
}

public sealed class CopilotFetchResult
{
    public CopilotAuthInfoDto? AuthInfo { get; set; }
    public IReadOnlyList<CopilotQuotaDto> Quotas { get; set; } = Array.Empty<CopilotQuotaDto>();
}

public interface ICopilotClientAdapter
{
    Task<CopilotFetchResult> FetchQuotasAsync(
        string executablePath,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
