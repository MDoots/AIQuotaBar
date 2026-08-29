namespace AIQuotaBar.Providers.ClaudeCode.Tests;

using AIQuotaBar.Core.Models;
using AIQuotaBar.Providers.ClaudeCode;
using AIQuotaBar.Providers.ClaudeCode.Transport;
using Xunit;
using Xunit.Abstractions;

public class ClaudeCodeUsageProviderTests
{
    private readonly ITestOutputHelper _output;

    public ClaudeCodeUsageProviderTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private sealed class MockClaudeProcessRunner : IClaudeProcessRunner
    {
        public ClaudeAuthStatusResult? AuthStatusResult { get; set; }
        public string UsageOutput { get; set; } = string.Empty;
        public Exception? ExceptionToThrow { get; set; }

        public Task<ClaudeAuthStatusResult?> CheckAuthStatusAsync(string executablePath, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow != null) throw ExceptionToThrow;
            return Task.FromResult(AuthStatusResult);
        }

        public Task<string> CaptureUsageAsync(string executablePath, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow != null) throw ExceptionToThrow;
            return Task.FromResult(UsageOutput);
        }
    }

    [Fact]
    public async Task GetUsageAsync_WhenExecutableMissing_ReturnsUnavailable()
    {
        var provider = new ClaudeCodeUsageProvider(executableLocator: () => null);

        var snapshot = await provider.GetUsageAsync();

        Assert.Equal(ProviderStatus.Unavailable, snapshot.Status);
        Assert.Equal("Claude Code executable not found on system", snapshot.StatusMessage);
    }

    [Fact]
    public async Task GetUsageAsync_WhenLoggedOut_ReturnsUnauthenticatedWithoutCallingCapture()
    {
        var runner = new MockClaudeProcessRunner
        {
            AuthStatusResult = new ClaudeAuthStatusResult
            {
                LoggedIn = false
            }
        };

        var provider = new ClaudeCodeUsageProvider(
            runner: runner,
            executableLocator: () => @"C:\npm\claude.cmd");

        var snapshot = await provider.GetUsageAsync();

        Assert.Equal(ProviderStatus.Unauthenticated, snapshot.Status);
        Assert.Equal("Claude Code requires sign-in", snapshot.StatusMessage);
    }

    [Fact]
    public async Task GetUsageAsync_WhenLoggedIn_CapturesAndNormalizes()
    {
        var runner = new MockClaudeProcessRunner
        {
            AuthStatusResult = new ClaudeAuthStatusResult
            {
                LoggedIn = true,
                SubscriptionTier = "Claude Pro"
            },
            UsageOutput = "Current session allowance: 20% used"
        };

        var provider = new ClaudeCodeUsageProvider(
            runner: runner,
            executableLocator: () => @"C:\npm\claude.cmd");

        var snapshot = await provider.GetUsageAsync();

        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Equal("Claude Pro", snapshot.AccountPlan);
        Assert.Single(snapshot.Windows);
        Assert.Equal(80.0, snapshot.Windows[0].RemainingPercent);
    }

    [Fact]
    public async Task GetUsageAsync_WhenTimeout_ReturnsTimeout()
    {
        var runner = new MockClaudeProcessRunner
        {
            ExceptionToThrow = new TimeoutException("timeout")
        };

        var provider = new ClaudeCodeUsageProvider(
            runner: runner,
            executableLocator: () => @"C:\npm\claude.cmd");

        var snapshot = await provider.GetUsageAsync();

        Assert.Equal(ProviderStatus.Timeout, snapshot.Status);
        Assert.Equal("Claude Code did not respond", snapshot.StatusMessage);
    }

    [Fact]
    public async Task GetUsageAsync_UsageBasedAuth_ReturnsTruthfulNoQuotaSnapshot()
    {
        var runner = new MockClaudeProcessRunner
        {
            AuthStatusResult = new ClaudeAuthStatusResult { LoggedIn = true, AuthMethod = "api_key", ApiProvider = "anthropic" },
            UsageOutput = "Authenticated with API Key. No subscription limits apply."
        };

        var provider = new ClaudeCodeUsageProvider(
            runner: runner,
            executableLocator: () => @"C:\bin\claude.exe");

        var snapshot = await provider.GetUsageAsync();

        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Equal("Usage-based billing — no fixed Claude Code quota", snapshot.StatusMessage);
        Assert.Empty(snapshot.Windows);
    }
}
