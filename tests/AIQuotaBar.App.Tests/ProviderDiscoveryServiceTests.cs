namespace AIQuotaBar.App.Tests;

using AIQuotaBar.App.Providers;
using AIQuotaBar.Core.Interfaces;
using AIQuotaBar.Core.Models;
using Xunit;

public class ProviderDiscoveryServiceTests
{
    private sealed class StubUsageProvider : IUsageProvider
    {
        public string Id { get; }
        public string DisplayName { get; }

        public StubUsageProvider(string id, string displayName)
        {
            Id = id;
            DisplayName = displayName;
        }

        public Task<ProviderSnapshot> GetUsageAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProviderSnapshot(Id, DisplayName, ProviderStatus.Available));
    }

    [Fact]
    public async Task DiscoverSingleAsync_ReturnsDetected_WhenLocatorFindsExecutable()
    {
        var descriptor = new ProviderDescriptor
        {
            Id = "test_provider",
            DisplayName = "Test Provider",
            ShortDisplayName = "Test",
            RefreshInterval = TimeSpan.FromSeconds(60),
            CreateProvider = () => new StubUsageProvider("test_provider", "Test Provider"),
            LocateExecutable = () => @"C:\Tools\test.exe",
            SetupUri = new Uri("https://example.com/setup"),
            KnownQuotaWindows = Array.Empty<KnownQuotaWindowDescriptor>()
        };

        var service = new ProviderDiscoveryService();
        var result = await service.DiscoverSingleAsync(descriptor);

        Assert.Equal("test_provider", result.ProviderId);
        Assert.Equal(ProviderDiscoveryStatus.Detected, result.Status);
        Assert.Equal(@"C:\Tools\test.exe", result.DetectedExecutablePath);
        Assert.Null(result.SafeMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DiscoverSingleAsync_ReturnsNotDetected_WhenLocatorReturnsEmpty(string? path)
    {
        var descriptor = new ProviderDescriptor
        {
            Id = "test_provider",
            DisplayName = "Test Provider",
            ShortDisplayName = "Test",
            RefreshInterval = TimeSpan.FromSeconds(60),
            CreateProvider = () => new StubUsageProvider("test_provider", "Test Provider"),
            LocateExecutable = () => path,
            SetupUri = new Uri("https://example.com/setup"),
            KnownQuotaWindows = Array.Empty<KnownQuotaWindowDescriptor>()
        };

        var service = new ProviderDiscoveryService();
        var result = await service.DiscoverSingleAsync(descriptor);

        Assert.Equal("test_provider", result.ProviderId);
        Assert.Equal(ProviderDiscoveryStatus.NotDetected, result.Status);
        Assert.Null(result.DetectedExecutablePath);
    }

    [Fact]
    public async Task DiscoverSingleAsync_ReturnsError_WhenLocatorThrows()
    {
        var descriptor = new ProviderDescriptor
        {
            Id = "test_provider",
            DisplayName = "Test Provider",
            ShortDisplayName = "Test",
            RefreshInterval = TimeSpan.FromSeconds(60),
            CreateProvider = () => new StubUsageProvider("test_provider", "Test Provider"),
            LocateExecutable = () => throw new InvalidOperationException("Registry error"),
            SetupUri = new Uri("https://example.com/setup"),
            KnownQuotaWindows = Array.Empty<KnownQuotaWindowDescriptor>()
        };

        var service = new ProviderDiscoveryService();
        var result = await service.DiscoverSingleAsync(descriptor);

        Assert.Equal("test_provider", result.ProviderId);
        Assert.Equal(ProviderDiscoveryStatus.Error, result.Status);
        Assert.NotNull(result.SafeMessage);
        Assert.Contains("Test Provider", result.SafeMessage);
    }

    [Fact]
    public async Task DiscoverAsync_ScansMultipleDescriptors_AndAggregatesResults()
    {
        var d1 = new ProviderDescriptor
        {
            Id = "p1",
            DisplayName = "Provider 1",
            ShortDisplayName = "P1",
            RefreshInterval = TimeSpan.FromSeconds(60),
            CreateProvider = () => new StubUsageProvider("p1", "P1"),
            LocateExecutable = () => @"C:\p1.exe",
            SetupUri = new Uri("https://example.com/1"),
            KnownQuotaWindows = Array.Empty<KnownQuotaWindowDescriptor>()
        };

        var d2 = new ProviderDescriptor
        {
            Id = "p2",
            DisplayName = "Provider 2",
            ShortDisplayName = "P2",
            RefreshInterval = TimeSpan.FromSeconds(60),
            CreateProvider = () => new StubUsageProvider("p2", "P2"),
            LocateExecutable = () => null,
            SetupUri = new Uri("https://example.com/2"),
            KnownQuotaWindows = Array.Empty<KnownQuotaWindowDescriptor>()
        };

        var d3 = new ProviderDescriptor
        {
            Id = "p3",
            DisplayName = "Provider 3",
            ShortDisplayName = "P3",
            RefreshInterval = TimeSpan.FromSeconds(60),
            CreateProvider = () => new StubUsageProvider("p3", "P3"),
            LocateExecutable = () => throw new Exception("Disk error"),
            SetupUri = new Uri("https://example.com/3"),
            KnownQuotaWindows = Array.Empty<KnownQuotaWindowDescriptor>()
        };

        var service = new ProviderDiscoveryService();
        var results = await service.DiscoverAsync(new[] { d1, d2, d3 });

        Assert.Equal(3, results.Count);
        Assert.Equal(ProviderDiscoveryStatus.Detected, results[0].Status);
        Assert.Equal(ProviderDiscoveryStatus.NotDetected, results[1].Status);
        Assert.Equal(ProviderDiscoveryStatus.Error, results[2].Status);
    }
}
