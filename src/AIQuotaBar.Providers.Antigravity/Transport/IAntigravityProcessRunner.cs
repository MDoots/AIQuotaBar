namespace AIQuotaBar.Providers.Antigravity.Transport;

public interface IAntigravityProcessRunner
{
    Task<string> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
