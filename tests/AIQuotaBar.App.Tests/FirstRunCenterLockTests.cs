namespace AIQuotaBar.App.Tests;

using System.Drawing;
using AIQuotaBar.App.Layout;
using AIQuotaBar.App.Providers;
using AIQuotaBar.App.Settings;
using AIQuotaBar.App.ViewModels;
using AIQuotaBar.App.Views;
using AIQuotaBar.Core.Interfaces;
using AIQuotaBar.Core.Models;
using Xunit;

public class FirstRunCenterLockTests
{
    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                if (System.Windows.Application.ResourceAssembly == null)
                {
                    System.Windows.Application.ResourceAssembly = typeof(WidgetWindow).Assembly;
                }
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    [Fact]
    public void InitialCheckingWindow_CenterMatchesWorkAreaCenterExactly()
    {
        // 1920 x 1020 work area, initial checking window: 300 x 160 px
        var (x, y) = PositionHelper.CalculateCenteredPhysicalPosition(
            windowWidthPx: 300,
            windowHeightPx: 160,
            workAreaLeft: 0,
            workAreaTop: 0,
            workAreaRight: 1920,
            workAreaBottom: 1020);

        Assert.Equal((1920 - 300) / 2, x); // 810
        Assert.Equal((1020 - 160) / 2, y); // 430

        var windowCenterX = x + 300 / 2.0;
        var windowCenterY = y + 160 / 2.0;
        Assert.Equal(960.0, windowCenterX);
        Assert.Equal(510.0, windowCenterY);
    }

    [Fact]
    public void WindowGrowth_MaintainsExactCenterInvariant()
    {
        // Initial size: 280 x 140
        var (x1, y1) = PositionHelper.CalculateCenteredPhysicalPosition(280, 140, 0, 0, 1920, 1020);
        Assert.Equal(960.0, x1 + 280 / 2.0);
        Assert.Equal(510.0, y1 + 140 / 2.0);

        // Expanded size: 280 x 310
        var (x2, y2) = PositionHelper.CalculateCenteredPhysicalPosition(280, 310, 0, 0, 1920, 1020);
        Assert.Equal(960.0, x2 + 280 / 2.0);
        Assert.Equal(510.0, y2 + 310 / 2.0);

        // Centers must be strictly equal
        Assert.Equal(x1 + 280 / 2.0, x2 + 280 / 2.0);
        Assert.Equal(y1 + 140 / 2.0, y2 + 310 / 2.0);
    }

    [Fact]
    public void SequentialSizeChanges_CenterRemainsInvariantAcrossAllSteps()
    {
        var sizes = new (int width, int height)[]
        {
            (300, 160), // Step 1: Initial checking
            (350, 240), // Step 2: One provider detected
            (350, 386), // Step 3: Two providers detected
            (438, 483), // Step 4: Full layout with reset timers
        };

        const double expectedCenterX = 960.0;
        const double expectedCenterY = 510.0;

        foreach (var (w, h) in sizes)
        {
            var (x, y) = PositionHelper.CalculateCenteredPhysicalPosition(w, h, 0, 0, 1920, 1020);
            var actualCenterX = x + w / 2.0;
            var actualCenterY = y + h / 2.0;

            Assert.True(Math.Abs(expectedCenterX - actualCenterX) <= 0.5, $"X center {actualCenterX} deviated from {expectedCenterX}");
            Assert.True(Math.Abs(expectedCenterY - actualCenterY) <= 0.5, $"Y center {actualCenterY} deviated from {expectedCenterY}");
        }
    }

    [Fact]
    public async Task InitialStartupSettled_FiredWhenDiscoveryAndEligibleRefreshesComplete()
    {
        var mockProvider = new MockUsageProvider("codex", "OpenAI Codex", () => new ProviderSnapshot(
            "codex", "OpenAI Codex", ProviderStatus.Available,
            windows: new[] { new QuotaWindow("primary", "5-Hour", 20.0, TimeSpan.FromHours(5), null, QuotaWindowStatus.Active) }));

        var d1 = new ProviderDescriptor
        {
            Id = "codex",
            DisplayName = "OpenAI Codex",
            ShortDisplayName = "Codex",
            RefreshInterval = TimeSpan.FromMinutes(1),
            CreateProvider = () => mockProvider,
            LocateExecutable = () => @"C:\codex.exe",
            SetupUri = new Uri("https://openai.com/codex"),
            KnownQuotaWindows = Array.Empty<KnownQuotaWindowDescriptor>()
        };

        var discoveryService = new MockDiscoveryService(_ => new[]
        {
            new ProviderDiscoveryResult("codex", ProviderDiscoveryStatus.Detected, @"C:\codex.exe")
        });

        using var vm = new WidgetViewModel(new[] { d1 }, discoveryService);

        bool settledFired = false;
        vm.InitialStartupSettled += () => settledFired = true;

        await vm.DiscoverProvidersAsync(isStartup: true);

        Assert.False(vm.IsDiscoveringProviders);
        Assert.True(settledFired);
        Assert.Equal(1, mockProvider.RefreshCallCount);
    }

    [Fact]
    public void Window_CancelFirstRunAutoCentering_DisablesLockImmediately()
    {
        RunOnSta(() =>
        {
            var window = new WidgetWindow();
            window.EnableFirstRunAutoCentering();
            Assert.True(window.IsFirstRunAutoCenterActive);

            window.CancelFirstRunAutoCentering();
            Assert.False(window.IsFirstRunAutoCenterActive);
        });
    }

    [Fact]
    public void ReturningUser_DoesNotEnableFirstRunAutoCenteringByDefault()
    {
        RunOnSta(() =>
        {
            var window = new WidgetWindow();
            Assert.False(window.IsFirstRunAutoCenterActive);
        });
    }

    private sealed class MockUsageProvider : IUsageProvider
    {
        public string Id { get; }
        public string DisplayName { get; }
        public int RefreshCallCount { get; private set; }
        private readonly Func<ProviderSnapshot> _snapshotFactory;

        public MockUsageProvider(string id, string displayName, Func<ProviderSnapshot> snapshotFactory)
        {
            Id = id;
            DisplayName = displayName;
            _snapshotFactory = snapshotFactory;
        }

        public Task<ProviderSnapshot> GetUsageAsync(CancellationToken cancellationToken = default)
        {
            RefreshCallCount++;
            return Task.FromResult(_snapshotFactory());
        }
    }

    private sealed class MockDiscoveryService : IProviderDiscoveryService
    {
        private readonly Func<IReadOnlyList<ProviderDescriptor>, IReadOnlyList<ProviderDiscoveryResult>> _handler;

        public MockDiscoveryService(Func<IReadOnlyList<ProviderDescriptor>, IReadOnlyList<ProviderDiscoveryResult>> handler)
        {
            _handler = handler;
        }

        public Task<IReadOnlyList<ProviderDiscoveryResult>> DiscoverAsync(
            IReadOnlyList<ProviderDescriptor> descriptors,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_handler(descriptors));
        }

        public Task<ProviderDiscoveryResult> DiscoverSingleAsync(
            ProviderDescriptor descriptor,
            CancellationToken cancellationToken = default)
        {
            var results = _handler(new[] { descriptor });
            return Task.FromResult(results[0]);
        }
    }
}
