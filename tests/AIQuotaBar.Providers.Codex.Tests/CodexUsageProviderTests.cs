namespace AIQuotaBar.Providers.Codex.Tests;

using AIQuotaBar.Core.Models;
using AIQuotaBar.Providers.Codex.Transport;
using Xunit;

public class CodexUsageProviderTests
{
    private sealed class MockRunner : ICodexProcessRunner
    {
        private readonly Func<ICodexProcessSession, Task> _handler;

        public MockRunner(Func<ICodexProcessSession, Task> handler)
        {
            _handler = handler;
        }

        public async Task RunAsync(
            string executablePath,
            string arguments,
            Func<ICodexProcessSession, Task> sessionAction,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            await _handler(new StubSession());
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
        Assert.Contains("not found", snapshot.StatusMessage);
    }

    [Fact]
    public async Task GetUsageAsync_ReturnsTimeout_WhenProcessTimesOut()
    {
        var runner = new MockRunner(_ => throw new TimeoutException("Process timed out after 6s"));
        var provider = new CodexUsageProvider(
            processRunner: runner,
            executableLocator: () => @"C:\codex.exe");

        var snapshot = await provider.GetUsageAsync();

        Assert.Equal(ProviderStatus.Timeout, snapshot.Status);
        Assert.Contains("timed out", snapshot.StatusMessage);
    }

    [Fact]
    public async Task GetUsageAsync_ReturnsCancelled_WhenUserCancels()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var runner = new MockRunner(_ => throw new OperationCanceledException(cts.Token));
        var provider = new CodexUsageProvider(
            processRunner: runner,
            executableLocator: () => @"C:\codex.exe");

        var snapshot = await provider.GetUsageAsync(cts.Token);

        Assert.Equal(ProviderStatus.Cancelled, snapshot.Status);
        Assert.Contains("cancelled", snapshot.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetUsageAsync_ReturnsError_WhenExceptionThrown()
    {
        var runner = new MockRunner(_ => throw new InvalidOperationException("Fatal process crash"));
        var provider = new CodexUsageProvider(
            processRunner: runner,
            executableLocator: () => @"C:\codex.exe");

        var snapshot = await provider.GetUsageAsync();

        Assert.Equal(ProviderStatus.Error, snapshot.Status);
        Assert.Contains("Fatal process crash", snapshot.StatusMessage);
    }
}
