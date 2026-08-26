namespace AIQuotaBar.App.Tests;

using AIQuotaBar.App.ViewModels;
using AIQuotaBar.Core.Interfaces;
using AIQuotaBar.Core.Models;
using Xunit;

public class WidgetViewModelTests
{
    private sealed class MockUsageProvider : IUsageProvider
    {
        private readonly Func<CancellationToken, Task<ProviderSnapshot>> _handler;

        public string Id => "mock";
        public string DisplayName => "Mock Provider";

        public MockUsageProvider(Func<CancellationToken, Task<ProviderSnapshot>> handler)
        {
            _handler = handler;
        }

        public Task<ProviderSnapshot> GetUsageAsync(CancellationToken cancellationToken = default)
        {
            return _handler(cancellationToken);
        }
    }

    [Fact]
    public async Task RefreshAsync_UpdatesViewModel_WhenSuccessful()
    {
        var resetEpoch = DateTimeOffset.UtcNow.AddHours(3);
        var snapshot = new ProviderSnapshot(
            providerId: "mock",
            providerDisplayName: "Mock Provider",
            status: ProviderStatus.Available,
            accountPlan: "ChatGPT Plus",
            windows: new[]
            {
                new QuotaWindow("primary", "5-Hour", 30, TimeSpan.FromHours(5), resetEpoch)
            });

        var provider = new MockUsageProvider(_ => Task.FromResult(snapshot));
        using var vm = new WidgetViewModel(provider);

        await vm.RefreshAsync();

        Assert.Equal(ProviderStatus.Available, vm.Status);
        Assert.Equal("ChatGPT Plus", vm.AccountPlan);
        Assert.True(vm.HasAccountPlan);
        Assert.Single(vm.Windows);
        Assert.Equal(70, vm.Windows[0].RemainingPercent);
        Assert.Equal("70% remaining", vm.Windows[0].RemainingText);
        Assert.NotNull(vm.Windows[0].ExactResetTime);
        Assert.Contains("70% remaining", vm.Windows[0].TooltipText);
    }

    [Fact]
    public void ModeToggleCommand_TogglesCompactMode_AndFiresCallback()
    {
        using var vm = new WidgetViewModel(new MockUsageProvider(_ => Task.FromResult(new ProviderSnapshot("m", "M", ProviderStatus.Available))));
        
        bool? callbackValue = null;
        vm.CompactModeChanged = val => callbackValue = val;

        Assert.False(vm.IsCompactMode);
        Assert.Equal("▴", vm.ModeToggleText);

        vm.ToggleModeCommand.Execute(null);

        Assert.True(vm.IsCompactMode);
        Assert.Equal("▾", vm.ModeToggleText);
        Assert.True(callbackValue);

        vm.ToggleModeCommand.Execute(null);

        Assert.False(vm.IsCompactMode);
        Assert.Equal("▴", vm.ModeToggleText);
        Assert.False(callbackValue);
    }

    [Fact]
    public void IsAlwaysOnTop_FiresCallback_WhenChanged()
    {
        using var vm = new WidgetViewModel(new MockUsageProvider(_ => Task.FromResult(new ProviderSnapshot("m", "M", ProviderStatus.Available))));
        
        bool? callbackValue = null;
        vm.AlwaysOnTopChanged = val => callbackValue = val;

        Assert.True(vm.IsAlwaysOnTop);

        vm.IsAlwaysOnTop = false;

        Assert.False(vm.IsAlwaysOnTop);
        Assert.False(callbackValue);
    }

    [Fact]
    public async Task RefreshAsync_PreventsSimultaneousConcurrentRefreshes()
    {
        var fetchCount = 0;
        var tcs = new TaskCompletionSource<ProviderSnapshot>();

        var provider = new MockUsageProvider(async token =>
        {
            Interlocked.Increment(ref fetchCount);
            return await tcs.Task;
        });

        using var vm = new WidgetViewModel(provider);

        // Start first refresh
        var task1 = vm.RefreshAsync();
        Assert.True(vm.IsLoading);

        // Second call while first is still pending should be ignored
        var task2 = vm.RefreshAsync();

        // Release first
        tcs.SetResult(new ProviderSnapshot("m", "M", ProviderStatus.Available));
        await Task.WhenAll(task1, task2);

        Assert.Equal(1, fetchCount);
    }

    [Fact]
    public void Dispose_IsIdempotent_AndDisablesRefresh()
    {
        var provider = new MockUsageProvider(_ => Task.FromResult(new ProviderSnapshot("m", "M", ProviderStatus.Available)));
        var vm = new WidgetViewModel(provider);

        vm.Dispose();
        vm.Dispose(); // Second call must not throw

        Assert.False(vm.CanRefresh);
    }

    [Fact]
    public async Task RefreshAsync_DoesNotStartOrUpdate_WhenDisposed()
    {
        var providerCalled = false;
        var provider = new MockUsageProvider(_ =>
        {
            providerCalled = true;
            return Task.FromResult(new ProviderSnapshot("m", "M", ProviderStatus.Available));
        });

        var vm = new WidgetViewModel(provider);
        vm.Dispose();

        await vm.RefreshAsync();

        Assert.False(providerCalled);
        Assert.Empty(vm.Windows);
    }

    [Fact]
    public async Task RefreshAsync_DoesNotApplySnapshot_IfDisposedDuringFetch()
    {
        var tcs = new TaskCompletionSource<ProviderSnapshot>();
        var provider = new MockUsageProvider(async token =>
        {
            return await tcs.Task;
        });

        var vm = new WidgetViewModel(provider);
        var refreshTask = vm.RefreshAsync();

        // Dispose while fetch is pending
        vm.Dispose();

        // Complete the fetch
        tcs.SetResult(new ProviderSnapshot(
            "m", "M", ProviderStatus.Available,
            windows: new[] { new QuotaWindow("p", "5-Hour", 10, null, null) }));

        await refreshTask;

        // Verify snapshot was NOT applied to the disposed view model
        Assert.Empty(vm.Windows);
    }
}
