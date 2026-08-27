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

        public string Id { get; }
        public string DisplayName { get; }

        public MockUsageProvider(
            string id,
            string displayName,
            Func<CancellationToken, Task<ProviderSnapshot>> handler)
        {
            Id = id;
            DisplayName = displayName;
            _handler = handler;
        }

        public Task<ProviderSnapshot> GetUsageAsync(CancellationToken cancellationToken = default)
        {
            return _handler(cancellationToken);
        }
    }

    [Fact]
    public async Task RefreshAllAsync_UpdatesMultipleProvidersConcurrently()
    {
        var resetEpoch = DateTimeOffset.UtcNow.AddHours(3);

        var codexSnapshot = new ProviderSnapshot(
            providerId: "codex",
            providerDisplayName: "OpenAI Codex",
            status: ProviderStatus.Available,
            accountPlan: "ChatGPT Plus",
            windows: new[]
            {
                new QuotaWindow("codex_5h", "5-Hour", 30, TimeSpan.FromHours(5), resetEpoch)
            });

        var agySnapshot = new ProviderSnapshot(
            providerId: "antigravity",
            providerDisplayName: "Google Antigravity",
            status: ProviderStatus.Available,
            windows: new[]
            {
                new QuotaWindow("gemini_5h", "Gemini · 5-Hour", 27.2945, TimeSpan.FromHours(5), resetEpoch)
            });

        var codexProvider = new MockUsageProvider("codex", "OpenAI Codex", _ => Task.FromResult(codexSnapshot));
        var agyProvider = new MockUsageProvider("antigravity", "Google Antigravity", _ => Task.FromResult(agySnapshot));

        var codexSection = new ProviderSectionViewModel(codexProvider, TimeSpan.FromSeconds(60));
        var agySection = new ProviderSectionViewModel(agyProvider, TimeSpan.FromSeconds(180));

        using var vm = new WidgetViewModel(new[] { codexSection, agySection });

        await vm.RefreshAllAsync();

        Assert.Equal(2, vm.Providers.Count);

        // Codex section checks
        var codexVm = vm.Providers[0];
        Assert.Equal(ProviderStatus.Available, codexVm.Status);
        Assert.Equal("ChatGPT Plus", codexVm.AccountPlan);
        Assert.True(codexVm.HasAccountPlan);
        Assert.Equal("#10B981", codexVm.ProviderAccentColor);
        Assert.Single(codexVm.Windows);
        Assert.Equal(70.0, codexVm.Windows[0].RemainingPercent);
        Assert.Equal("70%", codexVm.Windows[0].RemainingText);
        Assert.Contains("70% quota remaining", codexVm.Windows[0].TooltipText);

        // Antigravity section checks
        var agyVm = vm.Providers[1];
        Assert.Equal(ProviderStatus.Available, agyVm.Status);
        Assert.Null(agyVm.AccountPlan);
        Assert.False(agyVm.HasAccountPlan);
        Assert.Equal("#38BDF8", agyVm.ProviderAccentColor);
        Assert.Single(agyVm.Windows);
        Assert.Equal(72.7055, agyVm.Windows[0].RemainingPercent);
        Assert.Equal("73%", agyVm.Windows[0].RemainingText);
        Assert.Equal("Gemini · 5-Hour", agyVm.Windows[0].DisplayName);
        Assert.Contains("72.7% quota remaining", agyVm.Windows[0].TooltipText);

        Assert.Contains("Updated", vm.LastUpdatedText);
    }

    [Fact]
    public async Task RefreshAllAsync_IsolatesProviderFailures()
    {
        // Provider 1 succeeds
        var codexSnapshot = new ProviderSnapshot(
            providerId: "codex",
            providerDisplayName: "OpenAI Codex",
            status: ProviderStatus.Available,
            windows: new[] { new QuotaWindow("p", "5-Hour", 10, null, null) });

        // Provider 2 fails with exception
        var codexProvider = new MockUsageProvider("codex", "OpenAI Codex", _ => Task.FromResult(codexSnapshot));
        var agyProvider = new MockUsageProvider("antigravity", "Google Antigravity", _ => throw new InvalidOperationException("CLI error"));

        var codexSection = new ProviderSectionViewModel(codexProvider, TimeSpan.FromSeconds(60));
        var agySection = new ProviderSectionViewModel(agyProvider, TimeSpan.FromSeconds(180));

        using var vm = new WidgetViewModel(new[] { codexSection, agySection });

        await vm.RefreshAllAsync();

        // Codex must be unaffected
        Assert.Equal(ProviderStatus.Available, codexSection.Status);
        Assert.Single(codexSection.Windows);

        // Antigravity has error but does not throw or crash widget
        Assert.Equal(ProviderStatus.Error, agySection.Status);
        Assert.True(agySection.HasStatusMessage);
        Assert.Empty(agySection.Windows);
    }

    [Fact]
    public void ModeToggleCommand_TogglesCompactMode_AndFiresCallback()
    {
        var provider = new MockUsageProvider("m", "M", _ => Task.FromResult(new ProviderSnapshot("m", "M", ProviderStatus.Available)));
        using var vm = new WidgetViewModel(provider);

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
        var provider = new MockUsageProvider("m", "M", _ => Task.FromResult(new ProviderSnapshot("m", "M", ProviderStatus.Available)));
        using var vm = new WidgetViewModel(provider);

        bool? callbackValue = null;
        vm.AlwaysOnTopChanged = val => callbackValue = val;

        Assert.True(vm.IsAlwaysOnTop);

        vm.IsAlwaysOnTop = false;

        Assert.False(vm.IsAlwaysOnTop);
        Assert.False(callbackValue);
    }

    [Fact]
    public void Dispose_IsIdempotent_AndCleansUpTimersAndSections()
    {
        var provider = new MockUsageProvider("m", "M", _ => Task.FromResult(new ProviderSnapshot("m", "M", ProviderStatus.Available)));
        var vm = new WidgetViewModel(provider);

        vm.Dispose();
        vm.Dispose(); // Must not throw on subsequent call

        Assert.False(vm.CanRefresh);
    }

    [Fact]
    public async Task RefreshAllAsync_DoesNotUpdate_WhenDisposed()
    {
        var providerCalled = false;
        var provider = new MockUsageProvider("m", "M", _ =>
        {
            providerCalled = true;
            return Task.FromResult(new ProviderSnapshot("m", "M", ProviderStatus.Available));
        });

        var vm = new WidgetViewModel(provider);
        vm.Dispose();

        await vm.RefreshAllAsync();

        Assert.False(providerCalled);
    }

    [Fact]
    public void WidgetWidth_UpdatesLayoutMode_AndPropagatesToChildren()
    {
        var snapshot = new ProviderSnapshot("p", "Provider", ProviderStatus.Available, windows: new[]
        {
            new QuotaWindow("w", "Gemini · 5-Hour", 20, TimeSpan.FromHours(5), null)
        });
        var provider = new MockUsageProvider("p", "Provider", _ => Task.FromResult(snapshot));
        var section = new ProviderSectionViewModel(provider, TimeSpan.FromSeconds(60));
        section.ApplySnapshot(snapshot);

        using var vm = new WidgetViewModel(new[] { section });

        double? widthFired = null;
        vm.WidgetWidthChanged = w => widthFired = w;

        // Default: 330px -> Full
        Assert.Equal(330.0, vm.WidgetWidth);
        Assert.Equal(AIQuotaBar.App.Layout.WidgetLayoutMode.Full, vm.LayoutMode);
        Assert.Equal(AIQuotaBar.App.Layout.WidgetLayoutMode.Full, section.LayoutMode);
        Assert.Equal(AIQuotaBar.App.Layout.WidgetLayoutMode.Full, section.Windows[0].LayoutMode);
        Assert.Equal("Gemini · 5-Hour", section.Windows[0].DisplayName);

        // Change width to 500px -> Full
        vm.WidgetWidth = 500;
        Assert.Equal(500.0, widthFired);
        Assert.Equal(AIQuotaBar.App.Layout.WidgetLayoutMode.Full, vm.LayoutMode);
        Assert.Equal(AIQuotaBar.App.Layout.WidgetLayoutMode.Full, section.LayoutMode);
        Assert.Equal(AIQuotaBar.App.Layout.WidgetLayoutMode.Full, section.Windows[0].LayoutMode);
        Assert.Equal("Gemini · 5-Hour", section.Windows[0].DisplayName);

        // Change width to 300px -> Compact
        vm.WidgetWidth = 300;
        Assert.Equal(AIQuotaBar.App.Layout.WidgetLayoutMode.Compact, vm.LayoutMode);
        Assert.Equal(AIQuotaBar.App.Layout.WidgetLayoutMode.Compact, section.LayoutMode);
        Assert.Equal(AIQuotaBar.App.Layout.WidgetLayoutMode.Compact, section.Windows[0].LayoutMode);
        Assert.Equal("Gemini · 5h", section.Windows[0].DisplayName);

        // Change width to 250px -> Minimal
        vm.WidgetWidth = 250;
        Assert.Equal(AIQuotaBar.App.Layout.WidgetLayoutMode.Minimal, vm.LayoutMode);
        Assert.Equal(AIQuotaBar.App.Layout.WidgetLayoutMode.Minimal, section.LayoutMode);
        Assert.Equal(AIQuotaBar.App.Layout.WidgetLayoutMode.Minimal, section.Windows[0].LayoutMode);
        Assert.Equal("Gemini · 5h", section.Windows[0].DisplayName);

        // Change width to 200px -> Micro
        vm.WidgetWidth = 200;
        Assert.Equal(AIQuotaBar.App.Layout.WidgetLayoutMode.Micro, vm.LayoutMode);
        Assert.Equal(AIQuotaBar.App.Layout.WidgetLayoutMode.Micro, section.LayoutMode);
        Assert.Equal(AIQuotaBar.App.Layout.WidgetLayoutMode.Micro, section.Windows[0].LayoutMode);
        Assert.Equal("G · 5h", section.Windows[0].DisplayName);
        Assert.False(vm.ShowFooter);
    }
}
