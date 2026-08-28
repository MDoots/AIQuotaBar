namespace AIQuotaBar.App.Tests;

using System.IO;
using AIQuotaBar.App.Settings;
using AIQuotaBar.App.ViewModels;
using Xunit;

public class SettingsNotificationTests
{
    [Fact]
    public void AppSettings_Default_LowQuotaNotificationsEnabledIsTrue()
    {
        var settings = new AppSettings();
        Assert.True(settings.LowQuotaNotificationsEnabled);
    }

    [Fact]
    public void BackwardCompatibility_LegacyJsonWithoutNotifications_DefaultsToTrue()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aiquotabar_legacy_notif_{Guid.NewGuid():N}.json");

        try
        {
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

            Assert.True(loaded.LowQuotaNotificationsEnabled);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void Persistence_SettingsRoundTrip_PreservesNotificationPreference()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aiquotabar_notif_roundtrip_{Guid.NewGuid():N}.json");

        try
        {
            var manager = new SettingsManager(tempPath);
            var settings = new AppSettings
            {
                LowQuotaNotificationsEnabled = false
            };

            manager.Save(settings);

            var loaded = manager.Load();
            Assert.False(loaded.LowQuotaNotificationsEnabled);

            loaded.LowQuotaNotificationsEnabled = true;
            manager.Save(loaded);

            var reloaded = manager.Load();
            Assert.True(reloaded.LowQuotaNotificationsEnabled);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void SettingsViewModel_LiveToggle_PersistsImmediatelyToDisk()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aiquotabar_vm_notif_{Guid.NewGuid():N}.json");

        try
        {
            var manager = new SettingsManager(tempPath);
            var settings = manager.Load();
            Assert.True(settings.LowQuotaNotificationsEnabled);

            var vm = new SettingsViewModel(settings, manager);
            Assert.True(vm.LowQuotaNotificationsEnabled);

            // Toggle to false
            vm.LowQuotaNotificationsEnabled = false;

            // Check that file was saved immediately
            var reloaded = manager.Load();
            Assert.False(reloaded.LowQuotaNotificationsEnabled);

            // Reset visibility defaults must preserve notification preference
            vm.ResetDefaults();
            Assert.False(vm.LowQuotaNotificationsEnabled);

            var reloadedAfterReset = manager.Load();
            Assert.False(reloadedAfterReset.LowQuotaNotificationsEnabled);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
