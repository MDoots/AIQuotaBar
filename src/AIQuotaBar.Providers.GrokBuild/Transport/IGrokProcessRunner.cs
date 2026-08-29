namespace AIQuotaBar.Providers.GrokBuild.Transport;

public interface IGrokProcessSession
{
    Task WriteLineAsync(string line, CancellationToken cancellationToken = default);
    Task<string?> ReadLineAsync(CancellationToken cancellationToken = default);
}

public interface IGrokProcessRunner
{
    Task RunAsync(
        string executablePath,
        string arguments,
        Func<IGrokProcessSession, CancellationToken, Task> sessionAction,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
