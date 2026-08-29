namespace AIQuotaBar.App.Tests;

using System.Windows;
using AIQuotaBar.App.Layout;
using AIQuotaBar.App.Providers;
using AIQuotaBar.App.Settings;
using AIQuotaBar.App.ViewModels;
using AIQuotaBar.App.Views;
using AIQuotaBar.Core.Interfaces;
using AIQuotaBar.Core.Models;
using Xunit;

public class WidgetDiscoveryTests
{
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
        private readonly Func<IReadOnlyList<ProviderDescriptor>, Task<IReadOnlyList<ProviderDiscoveryResult>>> _asyncHandler;

        public MockDiscoveryService(Func<IReadOnlyList<ProviderDescriptor>, IReadOnlyList<ProviderDiscoveryResult>> handler)
        {
            _asyncHandler = d => Task.FromResult(handler(d));
        }

        public MockDiscoveryService(Func<IReadOnlyList<ProviderDescriptor>, Task<IReadOnlyList<ProviderDiscoveryResult>>> asyncHandler)
        {
            _asyncHandler = asyncHandler;
        }

        public Task<IReadOnlyList<ProviderDiscoveryResult>> DiscoverAsync(
            IReadOnlyList<ProviderDescriptor> descriptors,
            CancellationToken cancellationToken = default)
        {
            return _asyncHandler(descriptors);
        }

        public async Task<ProviderDiscoveryResult> DiscoverSingleAsync(
            ProviderDescriptor descriptor,
            CancellationToken cancellationToken = default)
        {
            var results = await _asyncHandler(new[] { descriptor });
            return results[0];
        }
    }

    [Fact]
    public void ProviderSection_NotDetected_IsAbsentFromWidget()
    {
        var provider = new MockUsageProvider("codex", "OpenAI Codex", () => new ProviderSnapshot(
            "codex", "OpenAI Codex", ProviderStatus.Available,
            windows: new[] { new QuotaWindow("primary", "5-Hour", 20.0, TimeSpan.FromHours(5), null, QuotaWindowStatus.Active) }));

        var section = new ProviderSectionViewModel(provider, TimeSpan.FromSeconds(60), "Codex", ProviderDiscoveryStatus.NotDetected);

        Assert.False(section.ShouldDisplayInWidget);
        Assert.True(section.IsProviderNotDetected);
        Assert.False(section.IsProviderDetected);
    }

    [Fact]
    public void ProviderSection_DetectedWithWindows_IsVisibleInWidget()
    {
        var provider = new MockUsageProvider("codex", "OpenAI Codex", () => new ProviderSnapshot(
            "codex", "OpenAI Codex", ProviderStatus.Available,
            windows: new[] { new QuotaWindow("primary", "5-Hour", 20.0, TimeSpan.FromHours(5), null, QuotaWindowStatus.Active) }));

        var section = new ProviderSectionViewModel(provider, TimeSpan.FromSeconds(60), "Codex", ProviderDiscoveryStatus.Detected);
        section.ApplySnapshot(new ProviderSnapshot(
            "codex", "OpenAI Codex", ProviderStatus.Available,
            windows: new[] { new QuotaWindow("primary", "5-Hour", 20.0, TimeSpan.FromHours(5), null, QuotaWindowStatus.Active) }));

        Assert.True(section.ShouldDisplayInWidget);
        Assert.True(section.IsProviderDetected);
        Assert.False(section.IsProviderNotDetected);
    }

    [Fact]
    public void ProviderSection_DetectedWithUnauthenticated_IsVisibleInWidget()
    {
        var provider = new MockUsageProvider("codex", "OpenAI Codex", () => new ProviderSnapshot(
            "codex", "OpenAI Codex", ProviderStatus.Unauthenticated, statusMessage: "Codex account requires login"));

        var section = new ProviderSectionViewModel(provider, TimeSpan.FromSeconds(60), "Codex", ProviderDiscoveryStatus.Detected);
        section.ApplySnapshot(new ProviderSnapshot(
            "codex", "OpenAI Codex", ProviderStatus.Unauthenticated, statusMessage: "Codex account requires login"));

        Assert.True(section.ShouldDisplayInWidget);
        Assert.True(section.HasStatusMessage);
    }

    [Fact]
    public void ProviderSection_DetectedWithError_IsVisibleInWidget()
    {
        var provider = new MockUsageProvider("antigravity", "Google Antigravity", () => new ProviderSnapshot(
            "antigravity", "Google Antigravity", ProviderStatus.Error, statusMessage: "Unable to communicate with Antigravity CLI"));

        var section = new ProviderSectionViewModel(provider, TimeSpan.FromSeconds(180), "Antigravity", ProviderDiscoveryStatus.Detected);
        section.ApplySnapshot(new ProviderSnapshot(
            "antigravity", "Google Antigravity", ProviderStatus.Error, statusMessage: "Unable to communicate with Antigravity CLI"));

        Assert.True(section.ShouldDisplayInWidget);
        Assert.True(section.HasStatusMessage);
    }

    [Fact]
    public async Task WidgetViewModel_OneDetected_OneNotDetected_ShowsOnlyDetected()
    {
        var codexProvider = new MockUsageProvider("codex", "OpenAI Codex", () => new ProviderSnapshot(
            "codex", "OpenAI Codex", ProviderStatus.Available,
            windows: new[] { new QuotaWindow("primary", "5-Hour", 30.0, TimeSpan.FromHours(5), null, QuotaWindowStatus.Active) }));

        var agyProvider = new MockUsageProvider("antigravity", "Google Antigravity", () => new ProviderSnapshot(
            "antigravity", "Google Antigravity", ProviderStatus.Available,
            windows: new[] { new QuotaWindow("gemini_5h", "Gemini 5-Hour", 40.0, TimeSpan.FromHours(5), null, QuotaWindowStatus.Active) }));

        var d1 = new ProviderDescriptor
        {
            Id = "codex",
            DisplayName = "OpenAI Codex",
            ShortDisplayName = "Codex",
            RefreshInterval = TimeSpan.FromSeconds(60),
            CreateProvider = () => codexProvider,
            LocateExecutable = () => @"C:\codex.exe",
            SetupUri = new Uri("https://developers.openai.com/codex/cli/"),
            KnownQuotaWindows = Array.Empty<KnownQuotaWindowDescriptor>()
        };

        var d2 = new ProviderDescriptor
        {
            Id = "antigravity",
            DisplayName = "Google Antigravity",
            ShortDisplayName = "Antigravity",
            RefreshInterval = TimeSpan.FromSeconds(180),
            CreateProvider = () => agyProvider,
            LocateExecutable = () => null,
            SetupUri = new Uri("https://antigravity.google/download"),
            KnownQuotaWindows = Array.Empty<KnownQuotaWindowDescriptor>()
        };

        var discoveryService = new MockDiscoveryService(_ => new[]
        {
            new ProviderDiscoveryResult("codex", ProviderDiscoveryStatus.Detected, @"C:\codex.exe"),
            new ProviderDiscoveryResult("antigravity", ProviderDiscoveryStatus.NotDetected)
        });

        using var vm = new WidgetViewModel(new[] { d1, d2 }, discoveryService);

        await vm.DiscoverProvidersAsync(isStartup: true);

        Assert.Single(vm.VisibleProviders);
        Assert.Equal("codex", vm.VisibleProviders[0].ProviderId);
        Assert.False(vm.ShowEmptyState);
        Assert.False(vm.ShowZeroProvidersDetected);
        Assert.False(vm.ShowNoQuotaRowsSelected);
        Assert.Equal(1, codexProvider.RefreshCallCount);
        Assert.Equal(0, agyProvider.RefreshCallCount);
    }

    [Fact]
    public async Task WidgetViewModel_ZeroDetected_ShowsZeroProvidersDetectedState()
    {
        var d1 = new ProviderDescriptor
        {
            Id = "codex",
            DisplayName = "OpenAI Codex",
            ShortDisplayName = "Codex",
            RefreshInterval = TimeSpan.FromSeconds(60),
            CreateProvider = () => new MockUsageProvider("codex", "Codex", () => new ProviderSnapshot("codex", "Codex", ProviderStatus.Available)),
            LocateExecutable = () => null,
            SetupUri = new Uri("https://developers.openai.com/codex/cli/"),
            KnownQuotaWindows = Array.Empty<KnownQuotaWindowDescriptor>()
        };

        var d2 = new ProviderDescriptor
        {
            Id = "antigravity",
            DisplayName = "Google Antigravity",
            ShortDisplayName = "Antigravity",
            RefreshInterval = TimeSpan.FromSeconds(180),
            CreateProvider = () => new MockUsageProvider("antigravity", "Antigravity", () => new ProviderSnapshot("antigravity", "Antigravity", ProviderStatus.Available)),
            LocateExecutable = () => null,
            SetupUri = new Uri("https://antigravity.google/download"),
            KnownQuotaWindows = Array.Empty<KnownQuotaWindowDescriptor>()
        };

        var discoveryService = new MockDiscoveryService(_ => new[]
        {
            new ProviderDiscoveryResult("codex", ProviderDiscoveryStatus.NotDetected),
            new ProviderDiscoveryResult("antigravity", ProviderDiscoveryStatus.NotDetected)
        });

        using var vm = new WidgetViewModel(new[] { d1, d2 }, discoveryService);

        await vm.DiscoverProvidersAsync(isStartup: true);

        Assert.Empty(vm.VisibleProviders);
        Assert.True(vm.ShowEmptyState);
        Assert.True(vm.ShowZeroProvidersDetected);
        Assert.False(vm.ShowNoQuotaRowsSelected);
        Assert.False(vm.ShowCheckingProviders);
    }

    [Fact]
    public void WidgetViewModel_DetectedProviders_AllRowsHidden_ShowsNoQuotaRowsSelected()
    {
        var provider = new MockUsageProvider("codex", "OpenAI Codex", () => new ProviderSnapshot(
            "codex", "OpenAI Codex", ProviderStatus.Available,
            windows: new[] { new QuotaWindow("primary", "5-Hour", 30.0, TimeSpan.FromHours(5), null, QuotaWindowStatus.Active) }));

        var section = new ProviderSectionViewModel(provider, TimeSpan.FromSeconds(60), "Codex", ProviderDiscoveryStatus.Detected);
        section.ApplySnapshot(new ProviderSnapshot(
            "codex", "OpenAI Codex", ProviderStatus.Available,
            windows: new[] { new QuotaWindow("primary", "5-Hour", 30.0, TimeSpan.FromHours(5), null, QuotaWindowStatus.Active) }));

        using var vm = new WidgetViewModel(new[] { section });

        var settings = new AppSettings();
        settings.SetProviderVisible("codex", false);
        vm.UpdateVisibility(settings);

        Assert.Empty(vm.VisibleProviders);
        Assert.True(vm.ShowEmptyState);
        Assert.False(vm.ShowZeroProvidersDetected);
        Assert.True(vm.ShowNoQuotaRowsSelected);
    }

    [Fact]
    public void ProviderSection_ClearsStaleData_WhenTransitioningToNotDetected()
    {
        var provider = new MockUsageProvider("codex", "OpenAI Codex", () => new ProviderSnapshot(
            "codex", "OpenAI Codex", ProviderStatus.Available,
            accountPlan: "ChatGPT Plus",
            windows: new[] { new QuotaWindow("primary", "5-Hour", 50.0, TimeSpan.FromHours(5), null, QuotaWindowStatus.Active) }));

        var section = new ProviderSectionViewModel(provider, TimeSpan.FromSeconds(60), "Codex", ProviderDiscoveryStatus.Detected);
        section.ApplySnapshot(new ProviderSnapshot(
            "codex", "OpenAI Codex", ProviderStatus.Available,
            accountPlan: "ChatGPT Plus",
            windows: new[] { new QuotaWindow("primary", "5-Hour", 50.0, TimeSpan.FromHours(5), null, QuotaWindowStatus.Active) }));

        Assert.True(section.HasWindows);
        Assert.True(section.HasAccountPlan);
        Assert.True(section.ShouldDisplayInWidget);

        // Transition to NotDetected
        section.ApplyDiscoveryStatus(ProviderDiscoveryStatus.NotDetected);

        Assert.False(section.HasWindows);
        Assert.Empty(section.AllWindows);
        Assert.Empty(section.Windows);
        Assert.Empty(section.VisibleWindows);
        Assert.False(section.HasAccountPlan);
        Assert.Null(section.AccountPlan);
        Assert.False(section.ShouldDisplayInWidget);
        Assert.Null(section.LastRefreshedAt);
    }

    [Fact]
    public async Task WidgetViewModel_Rescan_DiscoversNewProvider_AndRefreshesIt()
    {
        var codexProvider = new MockUsageProvider("codex", "OpenAI Codex", () => new ProviderSnapshot(
            "codex", "OpenAI Codex", ProviderStatus.Available,
            windows: new[] { new QuotaWindow("primary", "5-Hour", 30.0, TimeSpan.FromHours(5), null, QuotaWindowStatus.Active) }));

        var d1 = new ProviderDescriptor
        {
            Id = "codex",
            DisplayName = "OpenAI Codex",
            ShortDisplayName = "Codex",
            RefreshInterval = TimeSpan.FromSeconds(60),
            CreateProvider = () => codexProvider,
            LocateExecutable = () => null,
            SetupUri = new Uri("https://developers.openai.com/codex/cli/"),
            KnownQuotaWindows = Array.Empty<KnownQuotaWindowDescriptor>()
        };

        var currentStatus = ProviderDiscoveryStatus.NotDetected;
        var discoveryService = new MockDiscoveryService(_ => new[]
        {
            new ProviderDiscoveryResult("codex", currentStatus, currentStatus == ProviderDiscoveryStatus.Detected ? @"C:\codex.exe" : null)
        });

        using var vm = new WidgetViewModel(new[] { d1 }, discoveryService);

        // First scan: NotDetected
        await vm.DiscoverProvidersAsync(isStartup: true);
        Assert.Equal(0, codexProvider.RefreshCallCount);
        Assert.Empty(vm.VisibleProviders);

        // Now user installs Codex and clicks Rescan:
        currentStatus = ProviderDiscoveryStatus.Detected;
        await vm.RescanProvidersAsync();

        Assert.Equal(1, codexProvider.RefreshCallCount);
        Assert.Single(vm.VisibleProviders);
        Assert.Equal("codex", vm.VisibleProviders[0].ProviderId);
    }

    [Fact]
    public async Task ProviderSection_RefreshAsync_SkipsWhenNotDetected()
    {
        var provider = new MockUsageProvider("codex", "OpenAI Codex", () => new ProviderSnapshot(
            "codex", "OpenAI Codex", ProviderStatus.Available,
            windows: new[] { new QuotaWindow("primary", "5-Hour", 30.0, TimeSpan.FromHours(5), null, QuotaWindowStatus.Active) }));

        var section = new ProviderSectionViewModel(provider, TimeSpan.FromSeconds(60), "Codex", ProviderDiscoveryStatus.NotDetected);

        await section.RefreshAsync();

        Assert.Equal(0, provider.RefreshCallCount);
    }

    [Fact]
    public async Task ProviderSection_InFlightRefresh_DoesNotApplySnapshot_WhenTransitionedToNotDetected()
    {
        var tcs = new TaskCompletionSource<ProviderSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);

        var provider = new AsyncDelayUsageProvider("codex", "OpenAI Codex", () => tcs.Task);

        var section = new ProviderSectionViewModel(provider, TimeSpan.FromSeconds(60), "Codex", ProviderDiscoveryStatus.Detected);
        section.ApplySnapshot(new ProviderSnapshot(
            "codex", "OpenAI Codex", ProviderStatus.Available,
            windows: new[] { new QuotaWindow("primary", "5-Hour", 50.0, TimeSpan.FromHours(5), null, QuotaWindowStatus.Active) }));

        var snapshotAppliedCount = 0;
        section.SnapshotApplied += () => snapshotAppliedCount++;

        // 1. Start in-flight refresh (which awaits tcs.Task)
        var refreshTask = section.RefreshAsync();

        // 2. Transition to NotDetected while refresh is in-flight
        section.ApplyDiscoveryStatus(ProviderDiscoveryStatus.NotDetected);

        Assert.Empty(section.AllWindows);
        Assert.Empty(section.Windows);
        Assert.Empty(section.VisibleWindows);
        Assert.False(section.ShouldDisplayInWidget);

        // 3. Complete the in-flight provider response
        tcs.SetResult(new ProviderSnapshot(
            "codex", "OpenAI Codex", ProviderStatus.Available,
            windows: new[] { new QuotaWindow("primary", "5-Hour", 90.0, TimeSpan.FromHours(5), null, QuotaWindowStatus.Active) }));

        await refreshTask;

        // 4. Must NOT have repopulated rows or fired SnapshotApplied
        Assert.Empty(section.AllWindows);
        Assert.Empty(section.Windows);
        Assert.Empty(section.VisibleWindows);
        Assert.False(section.ShouldDisplayInWidget);
        Assert.Equal(0, snapshotAppliedCount);
    }

    [Fact]
    public async Task WidgetViewModel_Rescan_RetriesUnauthenticatedProvider()
    {
        var status = ProviderStatus.Unauthenticated;
        var codexProvider = new MockUsageProvider("codex", "OpenAI Codex", () => new ProviderSnapshot(
            "codex",
            "OpenAI Codex",
            status,
            statusMessage: status == ProviderStatus.Unauthenticated ? "Sign in required" : null,
            windows: status == ProviderStatus.Available
                ? new[] { new QuotaWindow("primary", "5-Hour", 75.0, TimeSpan.FromHours(5), null, QuotaWindowStatus.Active) }
                : Array.Empty<QuotaWindow>()));

        var d1 = new ProviderDescriptor
        {
            Id = "codex",
            DisplayName = "OpenAI Codex",
            ShortDisplayName = "Codex",
            RefreshInterval = TimeSpan.FromSeconds(60),
            CreateProvider = () => codexProvider,
            LocateExecutable = () => @"C:\codex.exe",
            SetupUri = new Uri("https://developers.openai.com/codex/cli/"),
            KnownQuotaWindows = Array.Empty<KnownQuotaWindowDescriptor>()
        };

        var discoveryService = new MockDiscoveryService(_ => new[]
        {
            new ProviderDiscoveryResult("codex", ProviderDiscoveryStatus.Detected, @"C:\codex.exe")
        });

        using var vm = new WidgetViewModel(new[] { d1 }, discoveryService);

        // Initial startup discovery -> gets Unauthenticated
        await vm.DiscoverProvidersAsync(isStartup: true);
        Assert.Equal(1, codexProvider.RefreshCallCount);
        Assert.Equal(ProviderStatus.Unauthenticated, vm.Providers[0].Status);

        // User signs in externally, now provider returns Available
        status = ProviderStatus.Available;

        // User clicks Rescan
        await vm.RescanProvidersAsync();

        Assert.Equal(2, codexProvider.RefreshCallCount);
        Assert.Equal(ProviderStatus.Available, vm.Providers[0].Status);
        Assert.True(vm.Providers[0].HasWindows);
    }

    [Fact]
    public async Task WidgetViewModel_Rescan_RetriesErrorOrTimeoutProvider()
    {
        var status = ProviderStatus.Timeout;
        var agyProvider = new MockUsageProvider("antigravity", "Google Antigravity", () => new ProviderSnapshot(
            "antigravity",
            "Google Antigravity",
            status,
            statusMessage: status == ProviderStatus.Timeout ? "Timed out" : null,
            windows: status == ProviderStatus.Available
                ? new[] { new QuotaWindow("gemini_5h", "Gemini · 5-Hour", 60.0, TimeSpan.FromHours(5), null, QuotaWindowStatus.Active) }
                : Array.Empty<QuotaWindow>()));

        var d1 = new ProviderDescriptor
        {
            Id = "antigravity",
            DisplayName = "Google Antigravity",
            ShortDisplayName = "Antigravity",
            RefreshInterval = TimeSpan.FromSeconds(180),
            CreateProvider = () => agyProvider,
            LocateExecutable = () => @"C:\agy.exe",
            SetupUri = new Uri("https://antigravity.google/download"),
            KnownQuotaWindows = Array.Empty<KnownQuotaWindowDescriptor>()
        };

        var discoveryService = new MockDiscoveryService(_ => new[]
        {
            new ProviderDiscoveryResult("antigravity", ProviderDiscoveryStatus.Detected, @"C:\agy.exe")
        });

        using var vm = new WidgetViewModel(new[] { d1 }, discoveryService);

        await vm.DiscoverProvidersAsync(isStartup: true);
        Assert.Equal(1, agyProvider.RefreshCallCount);
        Assert.Equal(ProviderStatus.Timeout, vm.Providers[0].Status);

        // Provider recovers
        status = ProviderStatus.Available;
        await vm.RescanProvidersAsync();

        Assert.Equal(2, agyProvider.RefreshCallCount);
        Assert.Equal(ProviderStatus.Available, vm.Providers[0].Status);
        Assert.True(vm.Providers[0].HasWindows);
    }

    [Fact]
    public async Task WidgetViewModel_Rescan_LeavesConnectedProviderUntouched()
    {
        var codexProvider = new MockUsageProvider("codex", "OpenAI Codex", () => new ProviderSnapshot(
            "codex", "OpenAI Codex", ProviderStatus.Available,
            windows: new[] { new QuotaWindow("primary", "5-Hour", 80.0, TimeSpan.FromHours(5), null, QuotaWindowStatus.Active) }));

        var d1 = new ProviderDescriptor
        {
            Id = "codex",
            DisplayName = "OpenAI Codex",
            ShortDisplayName = "Codex",
            RefreshInterval = TimeSpan.FromSeconds(60),
            CreateProvider = () => codexProvider,
            LocateExecutable = () => @"C:\codex.exe",
            SetupUri = new Uri("https://developers.openai.com/codex/cli/"),
            KnownQuotaWindows = Array.Empty<KnownQuotaWindowDescriptor>()
        };

        var discoveryService = new MockDiscoveryService(_ => new[]
        {
            new ProviderDiscoveryResult("codex", ProviderDiscoveryStatus.Detected, @"C:\codex.exe")
        });

        using var vm = new WidgetViewModel(new[] { d1 }, discoveryService);

        // Startup discovery performs initial refresh -> Connected
        await vm.DiscoverProvidersAsync(isStartup: true);
        Assert.Equal(1, codexProvider.RefreshCallCount);
        Assert.Equal(ProviderStatus.Available, vm.Providers[0].Status);
        Assert.True(vm.Providers[0].HasWindows);

        // Rescan runs when already Connected -> locator runs, but no extra GetUsageAsync process spawn
        await vm.RescanProvidersAsync();

        Assert.Equal(1, codexProvider.RefreshCallCount);
    }

    [Fact]
    public async Task WidgetViewModel_StaleScan_OlderScanIgnoredWhenNewerCompletesFirst()
    {
        var scan1Tcs = new TaskCompletionSource<IReadOnlyList<ProviderDiscoveryResult>>(TaskCreationOptions.RunContinuationsAsynchronously);

        var d1 = new ProviderDescriptor
        {
            Id = "codex",
            DisplayName = "OpenAI Codex",
            ShortDisplayName = "Codex",
            RefreshInterval = TimeSpan.FromSeconds(60),
            CreateProvider = () => new MockUsageProvider("codex", "Codex", () => new ProviderSnapshot("codex", "Codex", ProviderStatus.Available)),
            LocateExecutable = () => @"C:\codex.exe",
            SetupUri = new Uri("https://example.com"),
            KnownQuotaWindows = Array.Empty<KnownQuotaWindowDescriptor>()
        };

        var scanCount = 0;
        var discoveryService = new MockDiscoveryService((Func<IReadOnlyList<ProviderDescriptor>, Task<IReadOnlyList<ProviderDiscoveryResult>>>)(_ =>
        {
            scanCount++;
            if (scanCount == 1)
            {
                return scan1Tcs.Task;
            }
            return Task.FromResult<IReadOnlyList<ProviderDiscoveryResult>>(new[] { new ProviderDiscoveryResult("codex", ProviderDiscoveryStatus.Detected, @"C:\codex.exe") });
        }));

        using var vm = new WidgetViewModel(new[] { d1 }, discoveryService);

        // Start slow scan 1
        var scan1Task = vm.DiscoverProvidersAsync(isStartup: true);

        // Start scan 2 which completes with Detected
        await vm.RescanProvidersAsync();
        Assert.Equal(ProviderDiscoveryStatus.Detected, vm.Providers[0].DiscoveryStatus);

        // Complete slow scan 1 with NotDetected
        scan1Tcs.SetResult(new[] { new ProviderDiscoveryResult("codex", ProviderDiscoveryStatus.NotDetected) });
        await scan1Task;

        // Provider must remain Detected from scan 2
        Assert.Equal(ProviderDiscoveryStatus.Detected, vm.Providers[0].DiscoveryStatus);
    }

    [Fact]
    public void WidgetViewModel_DiscoveryError_DisplaysStatusCard_AndDoesNotShowNoQuotaRowsSelected()
    {
        var provider = new MockUsageProvider("codex", "OpenAI Codex", () => new ProviderSnapshot(
            "codex", "OpenAI Codex", ProviderStatus.Unavailable));

        var section = new ProviderSectionViewModel(provider, TimeSpan.FromSeconds(60), "Codex", ProviderDiscoveryStatus.Error);
        section.ApplyDiscoveryStatus(ProviderDiscoveryStatus.Error);

        using var vm = new WidgetViewModel(new[] { section });

        // Error provider has status message so it displays in widget as an error card
        Assert.Single(vm.VisibleProviders);
        Assert.False(vm.ShowNoQuotaRowsSelected);
        Assert.False(vm.ShowZeroProvidersDetected);
        Assert.False(vm.HasAnyDetectedProviders);
    }

    [Fact]
    public void WidgetViewModel_DiscoveryError_WhenCardHidden_ShowsShowDiscoveryError_AndDoesNotShowNoQuotaRowsSelected()
    {
        var provider = new MockUsageProvider("codex", "OpenAI Codex", () => new ProviderSnapshot(
            "codex", "OpenAI Codex", ProviderStatus.Unavailable));

        var section = new ProviderSectionViewModel(provider, TimeSpan.FromSeconds(60), "Codex", ProviderDiscoveryStatus.Error)
        {
            IsVisibleByPreference = false
        };
        section.ApplyDiscoveryStatus(ProviderDiscoveryStatus.Error);

        using var vm = new WidgetViewModel(new[] { section });

        Assert.Empty(vm.VisibleProviders);
        Assert.True(vm.ShowDiscoveryError);
        Assert.False(vm.ShowNoQuotaRowsSelected);
        Assert.False(vm.ShowZeroProvidersDetected);
        Assert.False(vm.HasAnyDetectedProviders);
    }

    [Fact]
    public void WidgetViewModel_Startup_WithDescriptors_SynchronouslyEntersCheckingState()
    {
        var d1 = new ProviderDescriptor
        {
            Id = "codex",
            DisplayName = "OpenAI Codex",
            ShortDisplayName = "Codex",
            RefreshInterval = TimeSpan.FromSeconds(60),
            CreateProvider = () => new MockUsageProvider("codex", "Codex", () => new ProviderSnapshot("codex", "Codex", ProviderStatus.Available)),
            LocateExecutable = () => @"C:\codex.exe",
            SetupUri = new Uri("https://example.com"),
            KnownQuotaWindows = Array.Empty<KnownQuotaWindowDescriptor>()
        };

        var d2 = new ProviderDescriptor
        {
            Id = "antigravity",
            DisplayName = "Google Antigravity",
            ShortDisplayName = "Antigravity",
            RefreshInterval = TimeSpan.FromSeconds(180),
            CreateProvider = () => new MockUsageProvider("antigravity", "Antigravity", () => new ProviderSnapshot("antigravity", "Antigravity", ProviderStatus.Available)),
            LocateExecutable = () => @"C:\agy.exe",
            SetupUri = new Uri("https://example.com"),
            KnownQuotaWindows = Array.Empty<KnownQuotaWindowDescriptor>()
        };

        // Instantiation with descriptors, then Start() immediately and synchronously establishes Checking state
        using var vm = new WidgetViewModel(new[] { d1, d2 });
        vm.Start();

        Assert.True(vm.IsDiscoveringProviders);
        Assert.True(vm.ShowCheckingProviders);
        Assert.True(vm.ShowEmptyState);
        Assert.False(vm.ShowZeroProvidersDetected);
        Assert.False(vm.ShowDiscoveryError);
        Assert.False(vm.ShowNoQuotaRowsSelected);
        Assert.Empty(vm.VisibleProviders);
        Assert.Equal(2, vm.Providers.Count);
        Assert.Equal(ProviderDiscoveryStatus.Checking, vm.Providers[0].DiscoveryStatus);
        Assert.Equal(ProviderDiscoveryStatus.Checking, vm.Providers[1].DiscoveryStatus);
    }

    [Fact]
    public void WidgetViewModel_BeforeDiscoveryCompletes_MaintainsCheckingPresentation()
    {
        var discoveryTcs = new TaskCompletionSource<IReadOnlyList<ProviderDiscoveryResult>>(TaskCreationOptions.RunContinuationsAsynchronously);

        var d1 = new ProviderDescriptor
        {
            Id = "codex",
            DisplayName = "OpenAI Codex",
            ShortDisplayName = "Codex",
            RefreshInterval = TimeSpan.FromSeconds(60),
            CreateProvider = () => new MockUsageProvider("codex", "Codex", () => new ProviderSnapshot("codex", "Codex", ProviderStatus.Available)),
            LocateExecutable = () => @"C:\codex.exe",
            SetupUri = new Uri("https://example.com"),
            KnownQuotaWindows = Array.Empty<KnownQuotaWindowDescriptor>()
        };

        var discoveryService = new MockDiscoveryService((Func<IReadOnlyList<ProviderDescriptor>, Task<IReadOnlyList<ProviderDiscoveryResult>>>)(_ => discoveryTcs.Task));

        using var vm = new WidgetViewModel(new[] { d1 }, discoveryService);

        // Before Start or during Start before completion
        vm.Start();

        Assert.True(vm.IsDiscoveringProviders);
        Assert.True(vm.ShowCheckingProviders);
        Assert.True(vm.ShowEmptyState);
        Assert.False(vm.ShowZeroProvidersDetected);
        Assert.False(vm.ShowDiscoveryError);
        Assert.False(vm.ShowNoQuotaRowsSelected);
        Assert.Empty(vm.VisibleProviders);
    }

    [Fact]
    public async Task WidgetViewModel_DiscoveryCompletes_WithZeroProviders_TransitionsToZeroProviderOnboarding()
    {
        var discoveryTcs = new TaskCompletionSource<IReadOnlyList<ProviderDiscoveryResult>>(TaskCreationOptions.RunContinuationsAsynchronously);

        var d1 = new ProviderDescriptor
        {
            Id = "codex",
            DisplayName = "OpenAI Codex",
            ShortDisplayName = "Codex",
            RefreshInterval = TimeSpan.FromSeconds(60),
            CreateProvider = () => new MockUsageProvider("codex", "Codex", () => new ProviderSnapshot("codex", "Codex", ProviderStatus.Available)),
            LocateExecutable = () => @"C:\codex.exe",
            SetupUri = new Uri("https://example.com"),
            KnownQuotaWindows = Array.Empty<KnownQuotaWindowDescriptor>()
        };

        var discoveryService = new MockDiscoveryService((Func<IReadOnlyList<ProviderDescriptor>, Task<IReadOnlyList<ProviderDiscoveryResult>>>)(_ => discoveryTcs.Task));

        using var vm = new WidgetViewModel(new[] { d1 }, discoveryService);

        // Start establishes synchronous Checking state
        vm.Start();
        Assert.True(vm.IsDiscoveringProviders);
        Assert.True(vm.ShowCheckingProviders);
        Assert.False(vm.ShowZeroProvidersDetected);

        // Complete discovery with NotDetected
        discoveryTcs.SetResult(new[] { new ProviderDiscoveryResult("codex", ProviderDiscoveryStatus.NotDetected) });
        await vm.DiscoverProvidersAsync(isStartup: true);

        // After completion: Checking is false, ZeroProviders is true
        Assert.False(vm.IsDiscoveringProviders);
        Assert.False(vm.ShowCheckingProviders);
        Assert.True(vm.ShowZeroProvidersDetected);
        Assert.False(vm.ShowDiscoveryError);
        Assert.False(vm.ShowNoQuotaRowsSelected);
        Assert.Empty(vm.VisibleProviders);
    }

    [Fact]
    public async Task WidgetViewModel_DiscoveryCompletes_WithDetectedProviders_TransitionsToConnected()
    {
        var d1 = new ProviderDescriptor
        {
            Id = "codex",
            DisplayName = "OpenAI Codex",
            ShortDisplayName = "Codex",
            RefreshInterval = TimeSpan.FromSeconds(60),
            CreateProvider = () => new MockUsageProvider("codex", "Codex", () => new ProviderSnapshot(
                "codex", "Codex", ProviderStatus.Available,
                windows: new[] { new QuotaWindow("primary", "5-Hour", 10.0, TimeSpan.FromHours(5), null, QuotaWindowStatus.Active) })),
            LocateExecutable = () => @"C:\codex.exe",
            SetupUri = new Uri("https://example.com"),
            KnownQuotaWindows = Array.Empty<KnownQuotaWindowDescriptor>()
        };

        var discoveryService = new MockDiscoveryService(_ => new[] { new ProviderDiscoveryResult("codex", ProviderDiscoveryStatus.Detected, @"C:\codex.exe") });

        using var vm = new WidgetViewModel(new[] { d1 }, discoveryService);

        await vm.DiscoverProvidersAsync(isStartup: true);

        Assert.False(vm.IsDiscoveringProviders);
        Assert.False(vm.ShowCheckingProviders);
        Assert.False(vm.ShowZeroProvidersDetected);
        Assert.False(vm.ShowDiscoveryError);
        Assert.False(vm.ShowEmptyState);
        Assert.Single(vm.VisibleProviders);
        Assert.Equal(ProviderDiscoveryStatus.Detected, vm.VisibleProviders[0].DiscoveryStatus);
    }

    [Fact]
    public async Task WidgetViewModel_WithAllFiveProviders_PreservesCanonicalOrderAndAccents()
    {
        var descriptors = ProviderCatalog.All;
        Assert.Equal(5, descriptors.Count);

        var discoveryResults = descriptors.Select(d => new ProviderDiscoveryResult(d.Id, ProviderDiscoveryStatus.Detected, @"C:\" + d.Id + ".exe")).ToList();
        var discoveryService = new MockDiscoveryService(_ => discoveryResults);

        using var vm = new WidgetViewModel(descriptors, discoveryService);
        await vm.DiscoverProvidersAsync(isStartup: false);

        Assert.Equal(5, vm.Providers.Count);
        Assert.Equal("codex", vm.Providers[0].ProviderId);
        Assert.Equal("antigravity", vm.Providers[1].ProviderId);
        Assert.Equal("claude-code", vm.Providers[2].ProviderId);
        Assert.Equal("grok-build", vm.Providers[3].ProviderId);
        Assert.Equal("github-copilot", vm.Providers[4].ProviderId);

        Assert.Equal("#10B981", vm.Providers[0].ProviderAccentColor);
        Assert.Equal("#38BDF8", vm.Providers[1].ProviderAccentColor);
        Assert.Equal("#D97757", vm.Providers[2].ProviderAccentColor);
        Assert.Equal("#D1D5DB", vm.Providers[3].ProviderAccentColor);
        Assert.Equal("#A78BFA", vm.Providers[4].ProviderAccentColor);
    }

    private sealed class AsyncDelayUsageProvider : IUsageProvider
    {
        public string Id { get; }
        public string DisplayName { get; }
        private readonly Func<Task<ProviderSnapshot>> _taskFactory;

        public AsyncDelayUsageProvider(string id, string displayName, Func<Task<ProviderSnapshot>> taskFactory)
        {
            Id = id;
            DisplayName = displayName;
            _taskFactory = taskFactory;
        }

        public Task<ProviderSnapshot> GetUsageAsync(CancellationToken cancellationToken = default) =>
            _taskFactory();
    }
}
