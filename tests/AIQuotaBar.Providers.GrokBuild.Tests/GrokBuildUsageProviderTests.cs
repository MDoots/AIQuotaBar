namespace AIQuotaBar.Providers.GrokBuild.Tests;

using AIQuotaBar.Core.Models;
using AIQuotaBar.Providers.GrokBuild;
using AIQuotaBar.Providers.GrokBuild.Protocol;
using AIQuotaBar.Providers.GrokBuild.Transport;
using Xunit;

public class GrokBuildUsageProviderTests
{
    private sealed class MockGrokProcessRunner : IGrokProcessRunner
    {
        private readonly Func<IGrokProcessSession, CancellationToken, Task> _handler;

        public MockGrokProcessRunner(Func<IGrokProcessSession, CancellationToken, Task> handler)
        {
            _handler = handler;
        }

        public Task RunAsync(string executablePath, string arguments, Func<IGrokProcessSession, CancellationToken, Task> sessionAction, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return _handler(new StubSession(), cancellationToken);
        }

        private sealed class StubSession : IGrokProcessSession
        {
            public Task WriteLineAsync(string line, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task<string?> ReadLineAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        }
    }

    [Fact]
    public async Task GetUsageAsync_WhenExecutableMissing_ReturnsUnavailable()
    {
        var provider = new GrokBuildUsageProvider(executableLocator: () => null);

        var snapshot = await provider.GetUsageAsync();

        Assert.Equal(ProviderStatus.Unavailable, snapshot.Status);
        Assert.Equal("Grok Build executable not found on system", snapshot.StatusMessage);
    }

    [Fact]
    public async Task GetUsageAsync_WhenAuthFails_ReturnsUnauthenticated()
    {
        var runner = new MockGrokProcessRunner((_, _) => throw new GrokAuthException("token missing"));
        var provider = new GrokBuildUsageProvider(
            processRunner: runner,
            executableLocator: () => @"C:\bin\grok.exe");

        var snapshot = await provider.GetUsageAsync();

        Assert.Equal(ProviderStatus.Unauthenticated, snapshot.Status);
        Assert.Equal("Grok Build requires sign-in", snapshot.StatusMessage);
    }

    [Fact]
    public async Task GetUsageAsync_WhenTimeoutOccurs_ReturnsTimeout()
    {
        var runner = new MockGrokProcessRunner((_, _) => throw new TimeoutException("timeout"));
        var provider = new GrokBuildUsageProvider(
            processRunner: runner,
            executableLocator: () => @"C:\bin\grok.exe");

        var snapshot = await provider.GetUsageAsync();

        Assert.Equal(ProviderStatus.Timeout, snapshot.Status);
        Assert.Equal("Grok Build did not respond", snapshot.StatusMessage);
    }

    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public GrokBuildUsageProviderTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task GetUsageAsync_WhenRunnerThrowsUnexpectedException_ReturnsErrorSnapshot()
    {
        var runner = new MockGrokProcessRunner((_, _) => throw new InvalidOperationException("Fatal process transport error"));

        var provider = new GrokBuildUsageProvider(
            processRunner: runner,
            executableLocator: () => @"C:\bin\grok.exe");

        var snapshot = await provider.GetUsageAsync();

        Assert.Equal(ProviderStatus.Error, snapshot.Status);
        Assert.Equal("Unable to communicate with Grok Build", snapshot.StatusMessage);
    }
}
