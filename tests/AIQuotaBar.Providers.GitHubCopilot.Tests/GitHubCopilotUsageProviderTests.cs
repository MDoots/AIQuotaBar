namespace AIQuotaBar.Providers.GitHubCopilot.Tests;

using System.Reflection;
using AIQuotaBar.Core.Models;
using AIQuotaBar.Providers.GitHubCopilot;
using AIQuotaBar.Providers.GitHubCopilot.Adapter;
using Xunit;
using Xunit.Abstractions;

public class GitHubCopilotUsageProviderTests
{
    private readonly ITestOutputHelper _output;

    public GitHubCopilotUsageProviderTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private sealed class MockCopilotAdapter : ICopilotClientAdapter
    {
        private readonly Func<string, TimeSpan, CancellationToken, Task<CopilotFetchResult>> _handler;

        public MockCopilotAdapter(Func<string, TimeSpan, CancellationToken, Task<CopilotFetchResult>> handler)
        {
            _handler = handler;
        }

        public Task<CopilotFetchResult> FetchQuotasAsync(string executablePath, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return _handler(executablePath, timeout, cancellationToken);
        }
    }

    [Fact]
    public void AdapterInterface_ExposesZeroSessionOrPromptMethods()
    {
        var methods = typeof(ICopilotClientAdapter).GetMethods(BindingFlags.Public | BindingFlags.Instance);

        Assert.Single(methods);
        Assert.Equal(nameof(ICopilotClientAdapter.FetchQuotasAsync), methods[0].Name);

        var forbiddenNames = new[] { "session", "prompt", "message", "chat", "completion", "turn", "conversation" };
        foreach (var method in methods)
        {
            foreach (var forbidden in forbiddenNames)
            {
                Assert.DoesNotContain(forbidden, method.Name, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public async Task GetUsageAsync_WhenExecutableMissing_ReturnsUnavailable()
    {
        var provider = new GitHubCopilotUsageProvider(executableLocator: () => null);

        var snapshot = await provider.GetUsageAsync();

        Assert.Equal(ProviderStatus.Unavailable, snapshot.Status);
        Assert.Equal("GitHub Copilot executable not found on system", snapshot.StatusMessage);
    }

    [Fact]
    public async Task GetUsageAsync_WhenSuccess_ReturnsAvailable()
    {
        var adapter = new MockCopilotAdapter((_, _, _) => Task.FromResult(new CopilotFetchResult
        {
            AuthInfo = new CopilotAuthInfoDto { IsAuthenticated = true, Plan = "individual" },
            Quotas = new[]
            {
                new CopilotQuotaDto { Key = "premium", RemainingPercentage = 80.0, EntitlementRequests = 300 }
            }
        }));

        var provider = new GitHubCopilotUsageProvider(
            adapter: adapter,
            executableLocator: () => @"C:\bin\copilot.exe");

        var snapshot = await provider.GetUsageAsync();

        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Single(snapshot.Windows);
        Assert.Equal(80.0, snapshot.Windows[0].RemainingPercent);
    }

    [Fact]
    public async Task GetUsageAsync_WhenTimeout_ReturnsTimeout()
    {
        var adapter = new MockCopilotAdapter((_, _, _) => throw new TimeoutException("timeout"));
        var provider = new GitHubCopilotUsageProvider(
            adapter: adapter,
            executableLocator: () => @"C:\bin\copilot.exe");

        var snapshot = await provider.GetUsageAsync();

        Assert.Equal(ProviderStatus.Timeout, snapshot.Status);
        Assert.Equal("GitHub Copilot did not respond", snapshot.StatusMessage);
    }

    [Fact]
    public async Task GetUsageAsync_WhenAdapterThrowsUnexpectedException_ReturnsErrorSnapshot()
    {
        var adapter = new MockCopilotAdapter((_, _, _) => throw new InvalidOperationException("Fatal process transport error"));

        var provider = new GitHubCopilotUsageProvider(
            adapter: adapter,
            executableLocator: () => @"C:\bin\copilot.exe");

        var snapshot = await provider.GetUsageAsync();

        Assert.Equal(ProviderStatus.Error, snapshot.Status);
        Assert.Equal("Unable to communicate with GitHub Copilot", snapshot.StatusMessage);
    }
}
