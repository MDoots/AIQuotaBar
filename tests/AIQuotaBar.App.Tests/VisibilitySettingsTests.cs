namespace AIQuotaBar.App.Tests;

using System.IO;
using System.Text.Json;
using AIQuotaBar.App.Settings;
using AIQuotaBar.App.ViewModels;
using AIQuotaBar.Core.Interfaces;
using AIQuotaBar.Core.Models;
using Xunit;

public class VisibilitySettingsTests
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
    public void SettingsDefaults_MissingProviderAndRow_DefaultToVisible()
    {
        var settings = new AppSettings();

        Assert.True(settings.IsProviderVisible("codex"));
        Assert.True(settings.IsProviderVisible("antigravity"));
        Assert.True(settings.IsProviderVisible("unknown_provider"));

        Assert.True(settings.IsQuotaWindowVisible("codex", "primary"));
        Assert.True(settings.IsQuotaWindowVisible("codex", "secondary"));
        Assert.True(settings.IsQuotaWindowVisible("antigravity", "gemini_gemini-5h"));
        Assert.True(settings.IsQuotaWindowVisible("unknown_provider", "unknown_window"));
    }

    [Fact]
    public void Settings_NullDictionaries_HandleSafelyAndDefaultToVisible()
    {
        var settings = new AppSettings
        {
            ProviderVisibility = null!,
            QuotaWindowVisibility = null!
        };

        Assert.True(settings.IsProviderVisible("codex"));
        Assert.True(settings.IsQuotaWindowVisible("codex", "primary"));

        // Setting a value should re-initialize the dictionary safely
        settings.SetProviderVisible("codex", false);
        Assert.False(settings.IsProviderVisible("codex"));

        settings.SetQuotaWindowVisible("codex", "primary", false);
        Assert.False(settings.IsQuotaWindowVisible("codex", "primary"));
    }

    [Fact]
    public void ProviderVisibility_HidingProvider_OmitsFromVisibleProviders_AndReEnableRestores()
    {
        var snapshot = new ProviderSnapshot(
            providerId: "codex",
            providerDisplayName: "OpenAI Codex",
            status: ProviderStatus.Available,
            windows: new[]
            {
                new QuotaWindow("primary", "5-Hour", 30, TimeSpan.FromHours(5), null),
                new QuotaWindow("secondary", "Weekly", 50, TimeSpan.FromDays(7), null)
            });

        var provider = new MockUsageProvider("codex", "OpenAI Codex", _ => Task.FromResult(snapshot));
        var section = new ProviderSectionViewModel(provider, TimeSpan.FromMinutes(1));
        section.ApplySnapshot(snapshot);

        using var vm = new WidgetViewModel(new[] { section });
        var settings = new AppSettings();

        // Initially visible
        vm.UpdateVisibility(settings);
        Assert.Single(vm.VisibleProviders);
        Assert.Equal("codex", vm.VisibleProviders[0].ProviderId);
        Assert.False(vm.ShowEmptyState);

        // Hide provider
        settings.SetProviderVisible("codex", false);
        vm.UpdateVisibility(settings);
        Assert.Empty(vm.VisibleProviders);
        Assert.True(vm.ShowEmptyState);

        // Restore provider
        settings.SetProviderVisible("codex", true);
        vm.UpdateVisibility(settings);
        Assert.Single(vm.VisibleProviders);
        Assert.Equal("codex", vm.VisibleProviders[0].ProviderId);
        Assert.False(vm.ShowEmptyState);
    }

    [Fact]
    public void ProviderVisibility_TogglingProvider_PreservesChildRowPreferences()
    {
        var snapshot = new ProviderSnapshot(
            providerId: "antigravity",
            providerDisplayName: "Google Antigravity",
            status: ProviderStatus.Available,
            windows: new[]
            {
                new QuotaWindow("gemini_5h", "Gemini · 5-Hour", 20, TimeSpan.FromHours(5), null),
                new QuotaWindow("gemini_weekly", "Gemini · Weekly", 40, TimeSpan.FromDays(7), null),
                new QuotaWindow("claudegpt_5h", "Claude & GPT · 5-Hour", 10, TimeSpan.FromHours(5), null),
                new QuotaWindow("claudegpt_weekly", "Claude & GPT · Weekly", 15, TimeSpan.FromDays(7), null)
            });

        var provider = new MockUsageProvider("antigravity", "Google Antigravity", _ => Task.FromResult(snapshot));
        var section = new ProviderSectionViewModel(provider, TimeSpan.FromMinutes(1));
        section.ApplySnapshot(snapshot);

        using var vm = new WidgetViewModel(new[] { section });
        var settings = new AppSettings();

        // User hides Gemini Weekly and Claude/GPT Weekly
        settings.SetQuotaWindowVisible("antigravity", "gemini_weekly", false);
        settings.SetQuotaWindowVisible("antigravity", "claudegpt_weekly", false);
        vm.UpdateVisibility(settings);

        Assert.Equal(2, section.VisibleWindows.Count);
        Assert.Equal("gemini_5h", section.VisibleWindows[0].Id);
        Assert.Equal("claudegpt_5h", section.VisibleWindows[1].Id);

        // User hides Antigravity provider
        settings.SetProviderVisible("antigravity", false);
        vm.UpdateVisibility(settings);
        Assert.Empty(vm.VisibleProviders);

        // User shows Antigravity provider again -> Child row preferences must still be respected!
        settings.SetProviderVisible("antigravity", true);
        vm.UpdateVisibility(settings);

        Assert.Single(vm.VisibleProviders);
        Assert.Equal(2, section.VisibleWindows.Count);
        Assert.Equal("gemini_5h", section.VisibleWindows[0].Id);
        Assert.Equal("claudegpt_5h", section.VisibleWindows[1].Id);
    }

    [Fact]
    public void RowVisibility_HidingSingleRow_OmitsOnlyThatRow_AndDoesNotAffectSibling()
    {
        var snapshot = new ProviderSnapshot(
            providerId: "codex",
            providerDisplayName: "OpenAI Codex",
            status: ProviderStatus.Available,
            windows: new[]
            {
                new QuotaWindow("primary", "5-Hour", 30, TimeSpan.FromHours(5), null),
                new QuotaWindow("secondary", "Weekly", 50, TimeSpan.FromDays(7), null)
            });

        var provider = new MockUsageProvider("codex", "OpenAI Codex", _ => Task.FromResult(snapshot));
        var section = new ProviderSectionViewModel(provider, TimeSpan.FromMinutes(1));
        section.ApplySnapshot(snapshot);

        using var vm = new WidgetViewModel(new[] { section });
        var settings = new AppSettings();

        // Initially 2 visible windows
        vm.UpdateVisibility(settings);
        Assert.Equal(2, section.VisibleWindows.Count);

        // Hide Weekly
        settings.SetQuotaWindowVisible("codex", "secondary", false);
        vm.UpdateVisibility(settings);

        Assert.Single(section.VisibleWindows);
        Assert.Equal("primary", section.VisibleWindows[0].Id);
        Assert.Single(vm.VisibleProviders); // Provider remains visible because 1 row remains

        // Restore Weekly immediately (no network/CLI poll needed)
        settings.SetQuotaWindowVisible("codex", "secondary", true);
        vm.UpdateVisibility(settings);

        Assert.Equal(2, section.VisibleWindows.Count);
        Assert.Equal("primary", section.VisibleWindows[0].Id);
        Assert.Equal("secondary", section.VisibleWindows[1].Id);
    }

    [Fact]
    public void ZeroVisibleProvider_ActivatesEmptyState_WhenAllProvidersOrAllRowsAreHidden()
    {
        var codexSnapshot = new ProviderSnapshot("codex", "OpenAI Codex", ProviderStatus.Available, windows: new[]
        {
            new QuotaWindow("primary", "5-Hour", 30, null, null)
        });
        var agySnapshot = new ProviderSnapshot("antigravity", "Google Antigravity", ProviderStatus.Available, windows: new[]
        {
            new QuotaWindow("gemini_5h", "Gemini 5-Hour", 20, null, null)
        });

        var codexSection = new ProviderSectionViewModel(new MockUsageProvider("codex", "Codex", _ => Task.FromResult(codexSnapshot)), TimeSpan.FromMinutes(1));
        var agySection = new ProviderSectionViewModel(new MockUsageProvider("antigravity", "AGY", _ => Task.FromResult(agySnapshot)), TimeSpan.FromMinutes(1));
        codexSection.ApplySnapshot(codexSnapshot);
        agySection.ApplySnapshot(agySnapshot);

        using var vm = new WidgetViewModel(new[] { codexSection, agySection });
        var settings = new AppSettings();

        vm.UpdateVisibility(settings);
        Assert.False(vm.ShowEmptyState);
        Assert.Equal(2, vm.VisibleProviders.Count);

        // Hide all providers
        settings.SetProviderVisible("codex", false);
        settings.SetProviderVisible("antigravity", false);
        vm.UpdateVisibility(settings);

        Assert.True(vm.ShowEmptyState);
        Assert.Empty(vm.VisibleProviders);

        // Re-enable one provider
        settings.SetProviderVisible("codex", true);
        vm.UpdateVisibility(settings);

        Assert.False(vm.ShowEmptyState);
        Assert.Single(vm.VisibleProviders);

        // Hide the only row in codex
        settings.SetQuotaWindowVisible("codex", "primary", false);
        vm.UpdateVisibility(settings);

        Assert.True(vm.ShowEmptyState);
        Assert.Empty(vm.VisibleProviders);
    }

    [Fact]
    public void Persistence_SettingsRoundTrip_PreservesVisibilityDictionaries()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aiquotabar_vis_test_{Guid.NewGuid():N}.json");

        try
        {
            var manager = new SettingsManager(tempPath);
            var original = new AppSettings
            {
                WindowLeft = 200,
                WindowTop = 300,
                WidgetWidth = 330,
                IsAlwaysOnTop = true,
                IsCompactMode = false
            };

            original.SetProviderVisible("codex", true);
            original.SetProviderVisible("antigravity", false);
            original.SetQuotaWindowVisible("codex", "primary", true);
            original.SetQuotaWindowVisible("codex", "secondary", false);
            original.SetQuotaWindowVisible("antigravity", "gemini_gemini-5h", false);

            manager.Save(original);
            var loaded = manager.Load();

            Assert.True(loaded.IsProviderVisible("codex"));
            Assert.False(loaded.IsProviderVisible("antigravity"));
            Assert.True(loaded.IsQuotaWindowVisible("codex", "primary"));
            Assert.False(loaded.IsQuotaWindowVisible("codex", "secondary"));
            Assert.False(loaded.IsQuotaWindowVisible("antigravity", "gemini_gemini-5h"));
            Assert.True(loaded.IsQuotaWindowVisible("antigravity", "unknown_row")); // Default visible
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void BackwardCompatibility_LegacyJsonWithoutVisibility_LoadsWithSafeDefaults()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aiquotabar_legacy_test_{Guid.NewGuid():N}.json");

        try
        {
            // Legacy v0.2.0 settings format with no visibility properties
            var legacyJson = """
            {
              "WindowLeft": 150.0,
              "WindowTop": 250.0,
              "WidgetWidth": 330.0,
              "IsAlwaysOnTop": true,
              "IsCompactMode": false,
              "StartWithWindows": false
            }
            """;
            File.WriteAllText(tempPath, legacyJson);

            var manager = new SettingsManager(tempPath);
            var loaded = manager.Load();

            Assert.Equal(150.0, loaded.WindowLeft);
            Assert.Equal(250.0, loaded.WindowTop);
            Assert.Equal(330.0, loaded.WidgetWidth);
            Assert.True(loaded.IsAlwaysOnTop);
            Assert.False(loaded.IsCompactMode);
            Assert.False(loaded.StartWithWindows);

            // Dictionaries are safely instantiated and return default visible
            Assert.NotNull(loaded.ProviderVisibility);
            Assert.NotNull(loaded.QuotaWindowVisibility);
            Assert.True(loaded.IsProviderVisible("codex"));
            Assert.True(loaded.IsProviderVisible("antigravity"));
            Assert.True(loaded.IsQuotaWindowVisible("codex", "primary"));
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void SettingsViewModel_LiveUpdate_TogglingCheckboxUpdatesWidgetAndSavesSettings()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aiquotabar_vm_test_{Guid.NewGuid():N}.json");

        try
        {
            var manager = new SettingsManager(tempPath);
            var settings = manager.Load();

            var snapshot = new ProviderSnapshot("codex", "OpenAI Codex", ProviderStatus.Available, windows: new[]
            {
                new QuotaWindow("primary", "5-Hour", 30, null, null),
                new QuotaWindow("secondary", "Weekly", 50, null, null)
            });

            var section = new ProviderSectionViewModel(new MockUsageProvider("codex", "Codex", _ => Task.FromResult(snapshot)), TimeSpan.FromMinutes(1));
            section.ApplySnapshot(snapshot);

            using var widgetVm = new WidgetViewModel(new[] { section });
            widgetVm.UpdateVisibility(settings);

            var settingsVm = new SettingsViewModel(settings, manager, widgetVm);

            Assert.Single(settingsVm.Providers);
            var codexItem = settingsVm.Providers[0];
            Assert.Equal("codex", codexItem.ProviderId);
            Assert.True(codexItem.IsVisible);
            Assert.Equal(2, codexItem.Windows.Count);

            // Toggle Weekly checkbox in Settings
            var weeklyItem = codexItem.Windows[1];
            Assert.Equal("secondary", weeklyItem.WindowId);
            Assert.True(weeklyItem.IsVisible);

            weeklyItem.IsVisible = false;

            // Widget should immediately update without polling
            Assert.Single(section.VisibleWindows);
            Assert.Equal("primary", section.VisibleWindows[0].Id);

            // Settings should be saved to file
            var reloaded = manager.Load();
            Assert.False(reloaded.IsQuotaWindowVisible("codex", "secondary"));

            // Test Reset Defaults command
            settingsVm.ResetDefaultsCommand.Execute(null);

            Assert.True(codexItem.IsVisible);
            Assert.True(weeklyItem.IsVisible);
            Assert.Equal(2, section.VisibleWindows.Count);

            var reloadedAfterReset = manager.Load();
            Assert.True(reloadedAfterReset.IsQuotaWindowVisible("codex", "secondary"));
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Theory]
    [InlineData(150, AIQuotaBar.App.Layout.WidgetLayoutMode.Micro)]
    [InlineData(220, AIQuotaBar.App.Layout.WidgetLayoutMode.Minimal)]
    [InlineData(330, AIQuotaBar.App.Layout.WidgetLayoutMode.Full)]
    [InlineData(560, AIQuotaBar.App.Layout.WidgetLayoutMode.Full)]
    public void ResponsiveWidths_WithFilteredRows_MaintainsCorrectLayoutMode(double width, AIQuotaBar.App.Layout.WidgetLayoutMode expectedMode)
    {
        var snapshot = new ProviderSnapshot("antigravity", "Google Antigravity", ProviderStatus.Available, windows: new[]
        {
            new QuotaWindow("gemini_5h", "Gemini · 5-Hour", 20, TimeSpan.FromHours(5), null),
            new QuotaWindow("gemini_weekly", "Gemini · Weekly", 40, TimeSpan.FromDays(7), null),
            new QuotaWindow("claudegpt_5h", "Claude & GPT · 5-Hour", 10, TimeSpan.FromHours(5), null)
        });

        var section = new ProviderSectionViewModel(new MockUsageProvider("antigravity", "AGY", _ => Task.FromResult(snapshot)), TimeSpan.FromMinutes(1));
        section.ApplySnapshot(snapshot);

        using var vm = new WidgetViewModel(new[] { section });
        var settings = new AppSettings();
        // Hide one row
        settings.SetQuotaWindowVisible("antigravity", "gemini_weekly", false);
        vm.UpdateVisibility(settings);

        vm.WidgetWidth = width;

        Assert.Equal(expectedMode, vm.LayoutMode);
        Assert.Equal(expectedMode, section.LayoutMode);
        Assert.Equal(2, section.VisibleWindows.Count);
        foreach (var window in section.VisibleWindows)
        {
            Assert.Equal(expectedMode, window.LayoutMode);
        }
    }

    [Fact]
    public void ResponsiveWidths_ResizeCycle_PreservesLayoutAndVisibleWindows()
    {
        var snapshot = new ProviderSnapshot("codex", "OpenAI Codex", ProviderStatus.Available, windows: new[]
        {
            new QuotaWindow("primary", "5-Hour", 30, TimeSpan.FromHours(5), null),
            new QuotaWindow("secondary", "Weekly", 50, TimeSpan.FromDays(7), null)
        });

        var section = new ProviderSectionViewModel(new MockUsageProvider("codex", "Codex", _ => Task.FromResult(snapshot)), TimeSpan.FromMinutes(1));
        section.ApplySnapshot(snapshot);

        using var vm = new WidgetViewModel(new[] { section });
        var settings = new AppSettings();
        vm.UpdateVisibility(settings);

        // 330 (Full)
        vm.WidgetWidth = 330;
        Assert.Equal(AIQuotaBar.App.Layout.WidgetLayoutMode.Full, vm.LayoutMode);

        // Resize to 150 (Micro)
        vm.WidgetWidth = 150;
        Assert.Equal(AIQuotaBar.App.Layout.WidgetLayoutMode.Micro, vm.LayoutMode);
        Assert.Equal(2, section.VisibleWindows.Count);

        // Resize back to 330 (Full)
        vm.WidgetWidth = 330;
        Assert.Equal(AIQuotaBar.App.Layout.WidgetLayoutMode.Full, vm.LayoutMode);
        Assert.Equal(2, section.VisibleWindows.Count);
    }

    [Fact]
    public void CaseInsensitive_Deserialization_PreservesOrdinalIgnoreCaseComparer()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aiquotabar_casing_test_{Guid.NewGuid():N}.json");

        try
        {
            var mixedCaseJson = """
            {
              "ProviderVisibility": {
                "CoDeX": false,
                "AnTiGrAvItY": true
              },
              "QuotaWindowVisibility": {
                "CoDeX:PrImArY": false,
                "antigravity:GeMiNi_5h": false
              }
            }
            """;
            File.WriteAllText(tempPath, mixedCaseJson);

            var manager = new SettingsManager(tempPath);
            var loaded = manager.Load();

            // Provider lookups with varying casing
            Assert.False(loaded.IsProviderVisible("codex"));
            Assert.False(loaded.IsProviderVisible("CODEX"));
            Assert.False(loaded.IsProviderVisible("CoDeX"));
            Assert.True(loaded.IsProviderVisible("antigravity"));
            Assert.True(loaded.IsProviderVisible("ANTIGRAVITY"));
            Assert.True(loaded.IsProviderVisible("AnTiGrAvItY"));

            // Quota window lookups with varying casing
            Assert.False(loaded.IsQuotaWindowVisible("codex", "primary"));
            Assert.False(loaded.IsQuotaWindowVisible("CODEX", "PRIMARY"));
            Assert.False(loaded.IsQuotaWindowVisible("CoDeX", "PrImArY"));
            Assert.False(loaded.IsQuotaWindowVisible("antigravity", "gemini_5h"));
            Assert.False(loaded.IsQuotaWindowVisible("ANTIGRAVITY", "GEMINI_5H"));
            Assert.False(loaded.IsQuotaWindowVisible("antigravity", "GeMiNi_5h"));

            // Unspecified rows default to true regardless of lookup casing
            Assert.True(loaded.IsQuotaWindowVisible("codex", "secondary"));
            Assert.True(loaded.IsQuotaWindowVisible("CODEX", "SECONDARY"));
            Assert.True(loaded.IsQuotaWindowVisible("unknown_provider", "unknown_window"));
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task AllRowsHidden_MustRemainHidden_DuringRefreshAndStatusChanges()
    {
        var snapshot = new ProviderSnapshot("codex", "OpenAI Codex", ProviderStatus.Available, windows: new[]
        {
            new QuotaWindow("primary", "5-Hour", 30, null, null),
            new QuotaWindow("secondary", "Weekly", 50, null, null)
        });

        var tcs = new TaskCompletionSource<ProviderSnapshot>();
        var provider = new MockUsageProvider("codex", "OpenAI Codex", _ => tcs.Task);
        var section = new ProviderSectionViewModel(provider, TimeSpan.FromMinutes(1));
        section.ApplySnapshot(snapshot);

        using var vm = new WidgetViewModel(new[] { section });
        var settings = new AppSettings();

        // 1. User deliberately hides all rows for Codex
        settings.SetQuotaWindowVisible("codex", "primary", false);
        settings.SetQuotaWindowVisible("codex", "secondary", false);
        vm.UpdateVisibility(settings);

        Assert.Empty(section.VisibleWindows);
        Assert.Equal(2, section.AllWindows.Count);
        Assert.False(section.ShouldDisplayInWidget);
        Assert.Empty(vm.VisibleProviders);
        Assert.True(vm.ShowEmptyState);

        // 2. Trigger a refresh -> IsLoading becomes true
        var refreshTask = section.RefreshAsync();
        Assert.True(section.IsLoading);

        // Crucial invariant: Provider MUST remain omitted even while IsLoading is true!
        Assert.False(section.ShouldDisplayInWidget);
        Assert.Empty(vm.VisibleProviders);
        Assert.True(vm.ShowEmptyState);

        // Complete refresh with new snapshot
        tcs.SetResult(snapshot);
        await refreshTask;

        Assert.False(section.IsLoading);
        Assert.False(section.ShouldDisplayInWidget);
        Assert.Empty(vm.VisibleProviders);
        Assert.True(vm.ShowEmptyState);
    }

    [Fact]
    public void ProviderWithZeroSnapshotRows_ShowsLoadingAndStatus_WhenEnabled()
    {
        // Provider has not loaded any rows yet (startup or auth error)
        var provider = new MockUsageProvider("codex", "OpenAI Codex", _ => Task.FromResult(new ProviderSnapshot("codex", "OpenAI Codex", ProviderStatus.Unavailable)));
        var section = new ProviderSectionViewModel(provider, TimeSpan.FromMinutes(1));
        var settings = new AppSettings();

        using var vm = new WidgetViewModel(new[] { section });
        vm.UpdateVisibility(settings);

        Assert.Empty(section.AllWindows);
        Assert.Empty(section.VisibleWindows);

        // Initial state before snapshot returns: initial ShouldDisplayInWidget is false if not loading and no status
        Assert.False(section.ShouldDisplayInWidget);

        // When unauthenticated / error status message arrives
        var errorSnapshot = new ProviderSnapshot(
            providerId: "codex",
            providerDisplayName: "OpenAI Codex",
            status: ProviderStatus.Unauthenticated,
            statusMessage: "Codex account requires login",
            windows: Array.Empty<QuotaWindow>());

        section.ApplySnapshot(errorSnapshot);

        Assert.Empty(section.AllWindows);
        Assert.True(section.HasStatusMessage);
        Assert.True(section.ShouldDisplayInWidget);
        Assert.Single(vm.VisibleProviders);
        Assert.False(vm.ShowEmptyState);

        // If user hides the provider itself, it must disappear
        settings.SetProviderVisible("codex", false);
        vm.UpdateVisibility(settings);

        Assert.False(section.ShouldDisplayInWidget);
        Assert.Empty(vm.VisibleProviders);
        Assert.True(vm.ShowEmptyState);
    }

    [Fact]
    public void ResetVisibilityDefaults_ClearsDictionariesAndPreservesEmptyJson()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aiquotabar_reset_test_{Guid.NewGuid():N}.json");

        try
        {
            var manager = new SettingsManager(tempPath);
            var settings = new AppSettings();

            settings.SetProviderVisible("codex", false);
            settings.SetQuotaWindowVisible("codex", "primary", false);
            settings.SetQuotaWindowVisible("antigravity", "gemini_gemini-5h", false);

            manager.Save(settings);

            // Execute reset
            settings.ResetVisibilityDefaults();

            Assert.Empty(settings.ProviderVisibility);
            Assert.Empty(settings.QuotaWindowVisibility);

            manager.Save(settings);

            // Read the raw JSON file to verify it does not contain explicit "true" entries for every key
            var rawJson = File.ReadAllText(tempPath);
            Assert.DoesNotContain("\"codex\": true", rawJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"primary\": true", rawJson, StringComparison.OrdinalIgnoreCase);

            // Reload and verify default-visible semantics
            var reloaded = manager.Load();
            Assert.Empty(reloaded.ProviderVisibility);
            Assert.Empty(reloaded.QuotaWindowVisibility);
            Assert.True(reloaded.IsProviderVisible("codex"));
            Assert.True(reloaded.IsProviderVisible("antigravity"));
            Assert.True(reloaded.IsQuotaWindowVisible("codex", "primary"));
            Assert.True(reloaded.IsQuotaWindowVisible("codex", "secondary"));
            Assert.True(reloaded.IsQuotaWindowVisible("future_provider", "future_window"));
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
