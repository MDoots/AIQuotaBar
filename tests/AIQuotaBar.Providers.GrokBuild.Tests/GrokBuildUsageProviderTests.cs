namespace AIQuotaBar.Providers.GrokBuild.Tests;

using AIQuotaBar.Core.Models;
using AIQuotaBar.Providers.GrokBuild;
using AIQuotaBar.Providers.GrokBuild.Protocol;
using AIQuotaBar.Providers.GrokBuild.Transport;
using Xunit;
using Xunit.Abstractions;

public class GrokBuildUsageProviderTests
{
    private readonly ITestOutputHelper _output;

    public GrokBuildUsageProviderTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private sealed class MockGrokProcessRunner : IGrokProcessRunner
    {
        private readonly Func<IGrokProcessSession, CancellationToken, Task> _handler;

        public MockGrokProcessRunner(Func<IGrokProcessSession, CancellationToken, Task> handler)
        {
            _handler = handler;
        }

        public Task RunAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            Func<IGrokProcessSession, CancellationToken, Task> sessionAction,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
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

    [Fact]
    public void SelectNonInteractiveAuthMethod_WhenOnlyInteractiveMethods_ReturnsNull()
    {
        var initResult = new GrokInitializeResult
        {
            AuthMethods = new[]
            {
                new GrokAuthMethod { Id = "browser_oauth", Type = "oauth", Interactive = true },
                new GrokAuthMethod { Id = "manual_login", Interactive = true }
            }
        };

        var selected = GrokBuildUsageProvider.SelectNonInteractiveAuthMethod(initResult);

        Assert.Null(selected);
    }

    [Fact]
    public void SelectNonInteractiveAuthMethod_WhenCachedTokenAvailable_SelectsIt()
    {
        var initResult = new GrokInitializeResult
        {
            AuthMethods = new[]
            {
                new GrokAuthMethod { Id = "browser_oauth", Interactive = true },
                new GrokAuthMethod { Id = "cached_token", Interactive = false }
            }
        };

        var selected = GrokBuildUsageProvider.SelectNonInteractiveAuthMethod(initResult);

        Assert.Equal("cached_token", selected);
    }

    [Fact]
    public void SelectNonInteractiveAuthMethod_WhenDefaultIdSpecifiedAndNonInteractive_SelectsDefault()
    {
        var initResult = new GrokInitializeResult
        {
            AuthMethods = new[]
            {
                new GrokAuthMethod { Id = "custom_token", Type = "token", Interactive = false },
                new GrokAuthMethod { Id = "cached_token", Interactive = false }
            },
            Meta = new GrokInitializeMeta { DefaultAuthMethodId = "custom_token" }
        };

        var selected = GrokBuildUsageProvider.SelectNonInteractiveAuthMethod(initResult);

        Assert.Equal("custom_token", selected);
    }
}
