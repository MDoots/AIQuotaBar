namespace AIQuotaBar.Providers.Codex.Transport;

public interface ICodexProcessSession
{
    Task WriteLineAsync(string line, CancellationToken cancellationToken = default);
    Task<string?> ReadLineAsync(CancellationToken cancellationToken = default);
}

public interface ICodexProcessRunner
{
    Task RunAsync(
        string executablePath,
        string arguments,
        Func<ICodexProcessSession, CancellationToken, Task> sessionAction,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
