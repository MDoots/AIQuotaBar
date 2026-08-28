namespace AIQuotaBar.App.Tests;

using System.IO;
using System.Text.Json;
using AIQuotaBar.App.Layout;
using AIQuotaBar.App.Settings;
using AIQuotaBar.App.ViewModels;
using AIQuotaBar.Core.Interfaces;
using AIQuotaBar.Core.Models;
using Xunit;

public class WidgetDockModeTests
{
    [Fact]
    public void AppSettings_DefaultsDockModeToFloating()
    {
        var settings = new AppSettings();
        Assert.Equal(WidgetDockMode.Floating, settings.DockMode);
    }

    [Fact]
    public void Deserialization_LegacyJsonWithoutDockMode_DefaultsToFloating()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aiquotabar_dock_legacy_{Guid.NewGuid():N}.json");

        try
        {
            var legacyJson = """
            {
              "WindowLeft": 500.0,
              "WindowTop": 200.0,
              "WidgetWidth": 330.0,
              "IsAlwaysOnTop": true,
              "IsCompactMode": false
            }
            """;
            File.WriteAllText(tempPath, legacyJson);

            var manager = new SettingsManager(tempPath);
            var loaded = manager.Load();

            Assert.Equal(WidgetDockMode.Floating, loaded.DockMode);
            Assert.Equal(500.0, loaded.WindowLeft);
            Assert.Equal(200.0, loaded.WindowTop);
            Assert.Equal(330.0, loaded.WidgetWidth);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Theory]
    [InlineData("{\"DockMode\": \"UnknownMode\", \"WindowLeft\": 120.0}", WidgetDockMode.Floating)]
    [InlineData("{\"DockMode\": \"InvalidValue\", \"WindowLeft\": 120.0}", WidgetDockMode.Floating)]
    [InlineData("{\"DockMode\": 999, \"WindowLeft\": 120.0}", WidgetDockMode.Floating)]
    [InlineData("{\"DockMode\": -1, \"WindowLeft\": 120.0}", WidgetDockMode.Floating)]
    [InlineData("{\"DockMode\": \"Top\", \"WindowLeft\": 120.0}", WidgetDockMode.Top)]
    [InlineData("{\"DockMode\": \"top\", \"WindowLeft\": 120.0}", WidgetDockMode.Top)]
    [InlineData("{\"DockMode\": 1, \"WindowLeft\": 120.0}", WidgetDockMode.Top)]
    [InlineData("{\"DockMode\": \"Bottom\", \"WindowLeft\": 120.0}", WidgetDockMode.Bottom)]
    [InlineData("{\"DockMode\": \"bottom\", \"WindowLeft\": 120.0}", WidgetDockMode.Bottom)]
    [InlineData("{\"DockMode\": 2, \"WindowLeft\": 120.0}", WidgetDockMode.Bottom)]
    [InlineData("{\"DockMode\": \"Floating\", \"WindowLeft\": 120.0}", WidgetDockMode.Floating)]
    [InlineData("{\"DockMode\": 0, \"WindowLeft\": 120.0}", WidgetDockMode.Floating)]
    public void Deserialization_VariousDockModeValues_ParsesSafelyWithoutInvalidatingSettings(string json, WidgetDockMode expectedMode)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aiquotabar_dock_parse_{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(tempPath, json);

            var manager = new SettingsManager(tempPath);
            var loaded = manager.Load();

            Assert.Equal(expectedMode, loaded.DockMode);
            Assert.Equal(120.0, loaded.WindowLeft);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void SettingsRoundTrip_PreservesDockMode()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aiquotabar_dock_roundtrip_{Guid.NewGuid():N}.json");

        try
        {
            var manager = new SettingsManager(tempPath);
            var original = new AppSettings
            {
                WindowLeft = 400.0,
                WindowTop = 150.0,
                WidgetWidth = 280.0,
                DockMode = WidgetDockMode.Top
            };

            manager.Save(original);
            var loaded = manager.Load();

            Assert.Equal(WidgetDockMode.Top, loaded.DockMode);
            Assert.Equal(400.0, loaded.WindowLeft);
            Assert.Equal(150.0, loaded.WindowTop);
            Assert.Equal(280.0, loaded.WidgetWidth);

            // Switch to Bottom and save again
            loaded.DockMode = WidgetDockMode.Bottom;
            manager.Save(loaded);

            var reloaded = manager.Load();
            Assert.Equal(WidgetDockMode.Bottom, reloaded.DockMode);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void ResetVisibilityDefaults_DoesNotResetDockMode()
    {
        var settings = new AppSettings
        {
            DockMode = WidgetDockMode.Top
        };
        settings.SetProviderVisible("codex", false);

        settings.ResetVisibilityDefaults();

        Assert.Equal(WidgetDockMode.Top, settings.DockMode);
        Assert.True(settings.IsProviderVisible("codex"));
    }

    [Theory]
    [InlineData(WidgetDockMode.Floating, true, false, false, false)]
    [InlineData(WidgetDockMode.Top, false, true, true, false)]
    [InlineData(WidgetDockMode.Bottom, false, true, false, true)]
    public void WidgetViewModel_DockModeProperties_ReflectStateCorrectly(
        WidgetDockMode mode,
        bool expectedFloating,
        bool expectedDocked,
        bool expectedTop,
        bool expectedBottom)
    {
        using var vm = new WidgetViewModel();
        vm.DockMode = mode;

        Assert.Equal(mode, vm.DockMode);
        Assert.Equal(expectedFloating, vm.IsFloatingMode);
        Assert.Equal(expectedDocked, vm.IsDockedMode);
        Assert.Equal(expectedTop, vm.IsDockedTop);
        Assert.Equal(expectedBottom, vm.IsDockedBottom);
    }

    [Fact]
    public void WidgetViewModel_DockModeChanged_FiresOnceWithNewValue()
    {
        using var vm = new WidgetViewModel();
        var firedModes = new List<WidgetDockMode>();
        vm.DockModeChanged = mode => firedModes.Add(mode);

        vm.DockMode = WidgetDockMode.Top;
        vm.DockMode = WidgetDockMode.Bottom;
        vm.DockMode = WidgetDockMode.Floating;

        Assert.Equal(3, firedModes.Count);
        Assert.Equal(WidgetDockMode.Top, firedModes[0]);
        Assert.Equal(WidgetDockMode.Bottom, firedModes[1]);
        Assert.Equal(WidgetDockMode.Floating, firedModes[2]);
    }

    [Fact]
    public void CompactMode_IsPreservedAcrossDockModeTransitions()
    {
        using var vm = new WidgetViewModel
        {
            IsCompactMode = true
        };

        Assert.True(vm.IsCompactMode);

        // Switch to Top
        vm.DockMode = WidgetDockMode.Top;
        Assert.True(vm.IsCompactMode);

        // Switch to Bottom
        vm.DockMode = WidgetDockMode.Bottom;
        Assert.True(vm.IsCompactMode);

        // Return to Floating
        vm.DockMode = WidgetDockMode.Floating;
        Assert.True(vm.IsCompactMode);
    }

    [Fact]
    public void SettingsViewModel_RadioProperties_SynchronizeWithDockModeAndWidgetViewModel()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aiquotabar_settings_vm_dock_{Guid.NewGuid():N}.json");

        try
        {
            var manager = new SettingsManager(tempPath);
            var settings = new AppSettings { DockMode = WidgetDockMode.Floating };
            manager.Save(settings);

            using var widgetVm = new WidgetViewModel { DockMode = WidgetDockMode.Floating };
            using var settingsVm = new SettingsViewModel(settings, manager, widgetVm);

            Assert.True(settingsVm.IsFloatingDockMode);
            Assert.False(settingsVm.IsTopDockMode);
            Assert.False(settingsVm.IsBottomDockMode);

            // User selects "Dock to top" in Settings
            settingsVm.IsTopDockMode = true;

            Assert.False(settingsVm.IsFloatingDockMode);
            Assert.True(settingsVm.IsTopDockMode);
            Assert.False(settingsVm.IsBottomDockMode);
            Assert.Equal(WidgetDockMode.Top, settings.DockMode);
            Assert.Equal(WidgetDockMode.Top, widgetVm.DockMode);

            // Saved to file
            var loaded = manager.Load();
            Assert.Equal(WidgetDockMode.Top, loaded.DockMode);

            // Mode changed externally (e.g. from tray menu on widgetVm) -> settingsVm updates live
            widgetVm.DockMode = WidgetDockMode.Bottom;

            Assert.False(settingsVm.IsFloatingDockMode);
            Assert.False(settingsVm.IsTopDockMode);
            Assert.True(settingsVm.IsBottomDockMode);
            Assert.Equal(WidgetDockMode.Bottom, settingsVm.DockMode);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void SettingsViewModel_Dispose_UnsubscribesFromWidgetViewModelDockModeChanged()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aiquotabar_settings_vm_disp_{Guid.NewGuid():N}.json");

        try
        {
            var manager = new SettingsManager(tempPath);
            var settings = new AppSettings { DockMode = WidgetDockMode.Floating };
            using var widgetVm = new WidgetViewModel { DockMode = WidgetDockMode.Floating };

            var settingsVm = new SettingsViewModel(settings, manager, widgetVm);

            // Active subscription updates settingsVm
            widgetVm.DockMode = WidgetDockMode.Top;
            Assert.Equal(WidgetDockMode.Top, settingsVm.DockMode);
            Assert.True(settingsVm.IsTopDockMode);

            // Dispose settingsVm
            settingsVm.Dispose();

            // Subsequent widgetVm changes must NOT propagate to the disposed instance
            widgetVm.DockMode = WidgetDockMode.Bottom;
            Assert.Equal(WidgetDockMode.Top, settingsVm.DockMode);
            Assert.True(settingsVm.IsTopDockMode);
            Assert.False(settingsVm.IsBottomDockMode);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void AlwaysOnTop_IsIndependentAcrossAllDockModes()
    {
        var settings = new AppSettings
        {
            IsAlwaysOnTop = true,
            DockMode = WidgetDockMode.Floating
        };

        using var vm = new WidgetViewModel
        {
            IsAlwaysOnTop = settings.IsAlwaysOnTop,
            DockMode = settings.DockMode
        };

        Assert.True(vm.IsAlwaysOnTop);

        // Switch to Top Dock
        vm.DockMode = WidgetDockMode.Top;
        Assert.True(vm.IsAlwaysOnTop);
        Assert.True(settings.IsAlwaysOnTop);

        // Switch to Bottom Dock
        vm.DockMode = WidgetDockMode.Bottom;
        Assert.True(vm.IsAlwaysOnTop);
        Assert.True(settings.IsAlwaysOnTop);

        // Turn Always On Top OFF
        vm.IsAlwaysOnTop = false;
        settings.IsAlwaysOnTop = false;

        // Switch back to Floating
        vm.DockMode = WidgetDockMode.Floating;
        Assert.False(vm.IsAlwaysOnTop);
        Assert.False(settings.IsAlwaysOnTop);
    }

    [Fact]
    public void ResetVisibilityDefaults_ClearsVisibilityOverrides_AndPreservesAllOtherSettings()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aiquotabar_reset_full_{Guid.NewGuid():N}.json");

        try
        {
            var manager = new SettingsManager(tempPath);
            var settings = new AppSettings
            {
                WindowLeft = 350.0,
                WindowTop = 220.0,
                WidgetWidth = 280.0,
                DockMode = WidgetDockMode.Top,
                LowQuotaNotificationsEnabled = false,
                IsAlwaysOnTop = false,
                IsCompactMode = true,
                StartWithWindows = true
            };

            settings.SetProviderVisible("codex", false);
            settings.SetQuotaWindowVisible("codex", "primary", false);
            settings.SetQuotaWindowVisible("antigravity", "gemini_gemini-5h", false);
            manager.Save(settings);

            using var widgetVm = new WidgetViewModel { DockMode = WidgetDockMode.Top };
            using var settingsVm = new SettingsViewModel(settings, manager, widgetVm);

            // Execute reset visibility defaults via SettingsViewModel
            settingsVm.ResetDefaults();

            var reloaded = manager.Load();

            // 1. Visibility overrides MUST be cleared
            Assert.Empty(reloaded.ProviderVisibility);
            Assert.Empty(reloaded.QuotaWindowVisibility);
            Assert.True(reloaded.IsProviderVisible("codex"));
            Assert.True(reloaded.IsQuotaWindowVisible("codex", "primary"));
            Assert.True(reloaded.IsQuotaWindowVisible("antigravity", "gemini_gemini-5h"));

            // 2. All other settings MUST be preserved
            Assert.Equal(WidgetDockMode.Top, reloaded.DockMode);
            Assert.False(reloaded.LowQuotaNotificationsEnabled);
            Assert.False(reloaded.IsAlwaysOnTop);
            Assert.True(reloaded.IsCompactMode);
            Assert.True(reloaded.StartWithWindows);
            Assert.Equal(350.0, reloaded.WindowLeft);
            Assert.Equal(220.0, reloaded.WindowTop);
            Assert.Equal(280.0, reloaded.WidgetWidth);

            // 3. SettingsViewModel properties must reflect preserved state
            Assert.Equal(WidgetDockMode.Top, settingsVm.DockMode);
            Assert.False(settingsVm.LowQuotaNotificationsEnabled);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private sealed class TestUsageProvider : IUsageProvider
    {
        public string Id { get; }
        public string DisplayName { get; }

        public TestUsageProvider(string id, string displayName)
        {
            Id = id;
            DisplayName = displayName;
        }

        public Task<ProviderSnapshot> GetUsageAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderSnapshot(Id, DisplayName, ProviderStatus.Available));
        }
    }

    [Fact]
    public void AppSettings_DefaultsHorizontalAnchorAndAutoHide()
    {
        var settings = new AppSettings();
        Assert.Equal(0.5, settings.DockedHorizontalAnchor);
        Assert.True(settings.AutoHideDockedBar);
    }

    [Fact]
    public void Deserialization_LegacyJsonWithoutAnchorOrAutoHide_DefaultsCorrectly()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aiquotabar_dock_legacy_anchor_{Guid.NewGuid():N}.json");

        try
        {
            var legacyJson = """
            {
              "WindowLeft": 500.0,
              "WindowTop": 200.0,
              "WidgetWidth": 330.0,
              "DockMode": "Top"
            }
            """;
            File.WriteAllText(tempPath, legacyJson);

            var manager = new SettingsManager(tempPath);
            var loaded = manager.Load();

            Assert.Equal(WidgetDockMode.Top, loaded.DockMode);
            Assert.Equal(0.5, loaded.DockedHorizontalAnchor);
            Assert.True(loaded.AutoHideDockedBar);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Theory]
    [InlineData(-0.2, 0.0)]
    [InlineData(1.5, 1.0)]
    [InlineData(double.NaN, 0.5)]
    [InlineData(double.PositiveInfinity, 0.5)]
    public void AppSettings_NormalizeVisibilityDictionaries_ClampsAnchor(double inputAnchor, double expectedAnchor)
    {
        var settings = new AppSettings
        {
            DockedHorizontalAnchor = inputAnchor
        };
        settings.NormalizeVisibilityDictionaries();
        Assert.Equal(expectedAnchor, settings.DockedHorizontalAnchor);
    }

    [Fact]
    public void SettingsRoundTrip_PreservesAnchorAndAutoHide()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aiquotabar_dock_anchor_roundtrip_{Guid.NewGuid():N}.json");

        try
        {
            var manager = new SettingsManager(tempPath);
            var original = new AppSettings
            {
                DockMode = WidgetDockMode.Top,
                DockedHorizontalAnchor = 0.85,
                AutoHideDockedBar = false
            };

            manager.Save(original);
            var loaded = manager.Load();

            Assert.Equal(WidgetDockMode.Top, loaded.DockMode);
            Assert.Equal(0.85, loaded.DockedHorizontalAnchor);
            Assert.False(loaded.AutoHideDockedBar);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void WidgetViewModel_AutoHideAndCollapseState_PropertiesFunctionCorrectly()
    {
        using var vm = new WidgetViewModel();

        // Floating mode
        vm.DockMode = WidgetDockMode.Floating;
        vm.AutoHideDockedBar = true;
        Assert.False(vm.IsAutoHideActive);

        // Docked mode
        vm.DockMode = WidgetDockMode.Top;
        Assert.True(vm.IsAutoHideActive);

        // AutoHide disabled
        vm.AutoHideDockedBar = false;
        Assert.False(vm.IsAutoHideActive);

        // Re-enable and collapse
        vm.AutoHideDockedBar = true;
        Assert.True(vm.IsAutoHideActive);
        Assert.False(vm.IsDockCollapsed);
        Assert.True(vm.IsDockExpanded);

        vm.IsDockCollapsed = true;
        Assert.True(vm.IsDockCollapsed);
        Assert.False(vm.IsDockExpanded);
    }

    [Fact]
    public void SettingsViewModel_AutoHideDockedBar_UpdatesSettingsAndWidgetViewModel()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aiquotabar_settings_autohide_{Guid.NewGuid():N}.json");

        try
        {
            var manager = new SettingsManager(tempPath);
            var settings = new AppSettings { AutoHideDockedBar = true };
            using var widgetVm = new WidgetViewModel { AutoHideDockedBar = true };
            using var settingsVm = new SettingsViewModel(settings, manager, widgetVm);

            Assert.True(settingsVm.AutoHideDockedBar);

            settingsVm.AutoHideDockedBar = false;

            Assert.False(settings.AutoHideDockedBar);
            Assert.False(widgetVm.AutoHideDockedBar);

            var reloaded = manager.Load();
            Assert.False(reloaded.AutoHideDockedBar);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Theory]
    [InlineData("codex", "OpenAI Codex", "Codex")]
    [InlineData("antigravity", "Google Antigravity", "Antigravity")]
    [InlineData("future_ai", "Future AI Platform", "Future AI Platform")]
    public void ProviderSectionViewModel_DockedDisplayName_MapsCorrectly(
        string providerId,
        string displayName,
        string expectedDockedName)
    {
        var provider = new TestUsageProvider(providerId, displayName);
        using var section = new ProviderSectionViewModel(provider, TimeSpan.FromMinutes(1));

        Assert.Equal(expectedDockedName, section.DockedDisplayName);
        Assert.Equal(displayName, section.ProviderName);
    }

    [Fact]
    public void WidgetViewModel_CornerRadius_EdgeSpecificAttachedTreatments()
    {
        using var vm = new WidgetViewModel();

        // Top Mode: top corners square, bottom corners rounded
        vm.DockMode = WidgetDockMode.Top;
        vm.IsDockCollapsed = false;
        Assert.Equal(0, vm.DockedRootCornerRadius.TopLeft);
        Assert.Equal(0, vm.DockedRootCornerRadius.TopRight);
        Assert.Equal(8, vm.DockedRootCornerRadius.BottomLeft);
        Assert.Equal(8, vm.DockedRootCornerRadius.BottomRight);

        // Bottom Mode: top corners rounded, bottom corners square
        vm.DockMode = WidgetDockMode.Bottom;
        vm.IsDockCollapsed = false;
        Assert.Equal(8, vm.DockedRootCornerRadius.TopLeft);
        Assert.Equal(8, vm.DockedRootCornerRadius.TopRight);
        Assert.Equal(0, vm.DockedRootCornerRadius.BottomLeft);
        Assert.Equal(0, vm.DockedRootCornerRadius.BottomRight);

        // Top Mode Collapsed handle
        vm.DockMode = WidgetDockMode.Top;
        vm.IsDockCollapsed = true;
        Assert.Equal(0, vm.DockedHandleCornerRadius.TopLeft);
        Assert.Equal(0, vm.DockedHandleCornerRadius.TopRight);
        Assert.Equal(4, vm.DockedHandleCornerRadius.BottomLeft);
        Assert.Equal(4, vm.DockedHandleCornerRadius.BottomRight);
        Assert.Equal(System.Windows.VerticalAlignment.Top, vm.DockedHandleVerticalAlignment);

        // Bottom Mode Collapsed handle
        vm.DockMode = WidgetDockMode.Bottom;
        vm.IsDockCollapsed = true;
        Assert.Equal(4, vm.DockedHandleCornerRadius.TopLeft);
        Assert.Equal(4, vm.DockedHandleCornerRadius.TopRight);
        Assert.Equal(0, vm.DockedHandleCornerRadius.BottomLeft);
        Assert.Equal(0, vm.DockedHandleCornerRadius.BottomRight);
        Assert.Equal(System.Windows.VerticalAlignment.Bottom, vm.DockedHandleVerticalAlignment);
    }

    [Fact]
    public void WidgetViewModel_DockedRootMargin_IsZeroForEdgeAttachment()
    {
        using var vm = new WidgetViewModel();

        vm.DockMode = WidgetDockMode.Top;
        Assert.Equal(0, vm.DockedRootMargin.Left);
        Assert.Equal(0, vm.DockedRootMargin.Top);
        Assert.Equal(0, vm.DockedRootMargin.Right);
        Assert.Equal(0, vm.DockedRootMargin.Bottom);

        vm.DockMode = WidgetDockMode.Bottom;
        Assert.Equal(0, vm.DockedRootMargin.Left);
        Assert.Equal(0, vm.DockedRootMargin.Top);
        Assert.Equal(0, vm.DockedRootMargin.Right);
        Assert.Equal(0, vm.DockedRootMargin.Bottom);
    }

    [Fact]
    public void WidgetViewModel_AppTitle_PreservedForFloatingMode()
    {
        using var vm = new WidgetViewModel();
        Assert.Equal("AIQuotaBar", vm.AppTitleText);
    }

    [Fact]
    public void FirstRun_WithNoSettingsFile_ResolvesFloatingDockModeAndCenteredPosition()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aiquotabar_firstrun_{Guid.NewGuid():N}.json");
        var manager = new SettingsManager(tempPath);

        var isFirstRun = !manager.SettingsFileExists;
        Assert.True(isFirstRun);

        var settings = manager.Load();
        var initialDockMode = isFirstRun ? WidgetDockMode.Floating : settings.DockMode;
        Assert.Equal(WidgetDockMode.Floating, initialDockMode);

        var primaryBounds = new System.Drawing.Rectangle(0, 0, 1920, 1040);
        var (left, top) = PositionHelper.GetCenteredPosition(
            windowWidth: 300,
            windowHeight: 160,
            getPrimaryScreenBounds: () => primaryBounds);

        Assert.Equal(810, left);
        Assert.Equal(440, top);
    }

    [Fact]
    public void ReturningUser_WithPersistedBottomDockMode_PreservesBottomMode()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aiquotabar_returning_dock_{Guid.NewGuid():N}.json");

        try
        {
            var manager = new SettingsManager(tempPath);
            manager.Save(new AppSettings
            {
                DockMode = WidgetDockMode.Bottom,
                WindowLeft = 100,
                WindowTop = 200
            });

            var isFirstRun = !manager.SettingsFileExists;
            Assert.False(isFirstRun);

            var settings = manager.Load();
            var initialDockMode = isFirstRun ? WidgetDockMode.Floating : settings.DockMode;
            Assert.Equal(WidgetDockMode.Bottom, initialDockMode);

            var hostPos = DockingHelper.ResolveInitialDockedHostPosition(settings.WindowLeft, settings.WindowTop);
            Assert.Equal(100, hostPos.Left);
            Assert.Equal(200, hostPos.Top);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void ReturningUser_WithPersistedFloatingCoordinates_PreservesSavedPosition()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aiquotabar_returning_float_{Guid.NewGuid():N}.json");

        try
        {
            var manager = new SettingsManager(tempPath);
            manager.Save(new AppSettings
            {
                DockMode = WidgetDockMode.Floating,
                WindowLeft = 450,
                WindowTop = 150
            });

            var isFirstRun = !manager.SettingsFileExists;
            Assert.False(isFirstRun);

            var settings = manager.Load();
            var initialDockMode = isFirstRun ? WidgetDockMode.Floating : settings.DockMode;
            Assert.Equal(WidgetDockMode.Floating, initialDockMode);

            var safePos = PositionHelper.GetSafePosition(
                settings.WindowLeft,
                settings.WindowTop,
                windowWidth: 300,
                getScreenBounds: () => new[] { new System.Drawing.Rectangle(0, 0, 1920, 1080) },
                getPrimaryScreenBounds: () => new System.Drawing.Rectangle(0, 0, 1920, 1080));

            Assert.Equal(450, safePos.Left);
            Assert.Equal(150, safePos.Top);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
