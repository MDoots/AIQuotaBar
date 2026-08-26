namespace AIQuotaBar.Providers.Codex.Tests;

using AIQuotaBar.Core.Models;
using AIQuotaBar.Providers.Codex.Transport;
using Xunit;

public class CodexUsageProviderTests
{
    private sealed class MockRunner : ICodexProcessRunner
    {
        private readonly Func<ICodexProcessSession, CancellationToken, Task> _handler;

        public MockRunner(Func<ICodexProcessSession, CancellationToken, Task> handler)
        {
            _handler = handler;
        }

        public async Task RunAsync(
            string executablePath,
            string arguments,
            Func<ICodexProcessSession, CancellationToken, Task> sessionAction,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            await _handler(new StubSession(), cancellationToken);
        }
    }

    private sealed class StubSession : ICodexProcessSession
    {
        public Task WriteLineAsync(string line, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string?> ReadLineAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }

    [Fact]
    public async Task GetUsageAsync_ReturnsUnavailable_WhenExecutableNotFound()
    {
        var provider = new CodexUsageProvider(executableLocator: () => null);

        var snapshot = await provider.GetUsageAsync();

        Assert.Equal(ProviderStatus.Unavailable, snapshot.Status);
        Assert.Equal("Codex executable not found on system", snapshot.StatusMessage);
    }

    [Fact]
    public async Task GetUsageAsync_ReturnsTimeout_WhenProcessTimesOut()
    {
        var runner = new MockRunner((_, _) => throw new TimeoutException("Internal timeout"));
        var provider = new CodexUsageProvider(
            processRunner: runner,
            executableLocator: () => @"C:\codex.exe");

        var snapshot = await provider.GetUsageAsync();

        Assert.Equal(ProviderStatus.Timeout, snapshot.Status);
        Assert.Equal("Codex app-server did not respond", snapshot.StatusMessage);
    }

    [Fact]
    public async Task GetUsageAsync_ReturnsCancelled_WhenUserCancels()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var runner = new MockRunner((_, _) => throw new OperationCanceledException(cts.Token));
        var provider = new CodexUsageProvider(
            processRunner: runner,
            executableLocator: () => @"C:\codex.exe");

        var snapshot = await provider.GetUsageAsync(cts.Token);

        Assert.Equal(ProviderStatus.Cancelled, snapshot.Status);
        Assert.Equal("Refresh cancelled by user", snapshot.StatusMessage);
    }

    [Fact]
    public async Task GetUsageAsync_ReturnsSafeErrorMessage_AndDoesNotExposeSecrets()
    {
        // Simulate an exception with sensitive paths and tokens
        var runner = new MockRunner((_, _) => throw new InvalidOperationException("Failed at C:\\Users\\secret_user\\.codex\\auth.json with token=sk-secret123"));
        var provider = new CodexUsageProvider(
            processRunner: runner,
            executableLocator: () => @"C:\codex.exe");

        var snapshot = await provider.GetUsageAsync();

        Assert.Equal(ProviderStatus.Error, snapshot.Status);
        Assert.Equal("Unable to communicate with Codex", snapshot.StatusMessage);
        Assert.DoesNotContain("secret_user", snapshot.StatusMessage);
        Assert.DoesNotContain("sk-secret123", snapshot.StatusMessage);
    }
}
