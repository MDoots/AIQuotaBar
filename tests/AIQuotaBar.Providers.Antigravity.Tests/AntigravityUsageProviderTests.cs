namespace AIQuotaBar.Providers.Antigravity.Tests;

using AIQuotaBar.Core.Models;
using AIQuotaBar.Providers.Antigravity.Transport;
using Xunit;

public class AntigravityUsageProviderTests
{
    private sealed class MockRunner : IAntigravityProcessRunner
    {
        private readonly Func<IReadOnlyList<string>, CancellationToken, Task<string>> _handler;

        public MockRunner(Func<IReadOnlyList<string>, CancellationToken, Task<string>> handler)
        {
            _handler = handler;
        }

        public Task<string> RunAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            return _handler(arguments, cancellationToken);
        }
    }

    [Fact]
    public async Task GetUsageAsync_ReturnsUnavailable_WhenExecutableNotFound()
    {
        var provider = new AntigravityUsageProvider(executableLocator: () => null);

        var snapshot = await provider.GetUsageAsync();

        Assert.Equal(ProviderStatus.Unavailable, snapshot.Status);
        Assert.Equal("Antigravity CLI not found on system", snapshot.StatusMessage);
    }

    [Fact]
    public async Task GetUsageAsync_ReturnsSnapshot_WhenSuccessful()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "antigravity_usage_success.json");
        var jsonContent = await File.ReadAllTextAsync(fixturePath);

        var runner = new MockRunner((_, _) => Task.FromResult(jsonContent));
        var provider = new AntigravityUsageProvider(
            processRunner: runner,
            executableLocator: () => @"C:\agy\agy.exe");

        var snapshot = await provider.GetUsageAsync();

        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Null(snapshot.StatusMessage);
        Assert.Equal(4, snapshot.Windows.Count);
        Assert.Equal("Gemini · 5-Hour", snapshot.Windows[0].DisplayName);
    }

    [Fact]
    public async Task GetUsageAsync_ReturnsTimeout_WhenProcessTimesOut()
    {
        var runner = new MockRunner((_, _) => throw new TimeoutException("Process timed out"));
        var provider = new AntigravityUsageProvider(
            processRunner: runner,
            executableLocator: () => @"C:\agy\agy.exe");

        var snapshot = await provider.GetUsageAsync();

        Assert.Equal(ProviderStatus.Timeout, snapshot.Status);
        Assert.Equal("Antigravity CLI query timed out", snapshot.StatusMessage);
    }

    [Fact]
    public async Task GetUsageAsync_ReturnsCancelled_WhenUserCancels()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var runner = new MockRunner((_, _) => throw new OperationCanceledException(cts.Token));
        var provider = new AntigravityUsageProvider(
            processRunner: runner,
            executableLocator: () => @"C:\agy\agy.exe");

        var snapshot = await provider.GetUsageAsync(cts.Token);

        Assert.Equal(ProviderStatus.Cancelled, snapshot.Status);
        Assert.Equal("Refresh cancelled by user", snapshot.StatusMessage);
    }

    [Fact]
    public async Task GetUsageAsync_SanitizesSensitivePathsAndTokens_OnException()
    {
        // General error with sensitive path
        var runner = new MockRunner((_, _) => throw new InvalidOperationException("Failed at C:\\Users\\secret_user\\AppData\\Local\\agy\\internal.dat with code 0x80070005"));
        var provider = new AntigravityUsageProvider(
            processRunner: runner,
            executableLocator: () => @"C:\agy\agy.exe");

        var snapshot = await provider.GetUsageAsync();

        Assert.Equal(ProviderStatus.Error, snapshot.Status);
        Assert.Equal("Unable to communicate with Antigravity CLI", snapshot.StatusMessage);
        Assert.DoesNotContain("secret_user", snapshot.StatusMessage);

        // Auth-related error with sensitive token
        var authRunner = new MockRunner((_, _) => throw new InvalidOperationException("Failed auth for user secret_user with token=sk-token123"));
        var authProvider = new AntigravityUsageProvider(
            processRunner: authRunner,
            executableLocator: () => @"C:\agy\agy.exe");

        var authSnapshot = await authProvider.GetUsageAsync();

        Assert.Equal(ProviderStatus.Error, authSnapshot.Status);
        Assert.Equal("Antigravity CLI requires authentication", authSnapshot.StatusMessage);
        Assert.DoesNotContain("secret_user", authSnapshot.StatusMessage);
        Assert.DoesNotContain("sk-token123", authSnapshot.StatusMessage);
    }

    [Fact]
    public async Task GetUsageAsync_ReturnsError_WhenResponseIsMalformedJson()
    {
        var runner = new MockRunner((_, _) => Task.FromResult("Not valid json at all"));
        var provider = new AntigravityUsageProvider(
            processRunner: runner,
            executableLocator: () => @"C:\agy\agy.exe");

        var snapshot = await provider.GetUsageAsync();

        Assert.Equal(ProviderStatus.Error, snapshot.Status);
        Assert.Equal("Antigravity returned an unexpected response format", snapshot.StatusMessage);
    }
}
