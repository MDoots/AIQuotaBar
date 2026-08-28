namespace AIQuotaBar.App.Tests;

using System.IO;
using AIQuotaBar.App.Settings;
using Xunit;

public class SettingsManagerTests
{
    [Fact]
    public void Load_ReturnsDefaultSettings_WhenFileDoesNotExist()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aiquotabar_test_{Guid.NewGuid():N}.json");
        var manager = new SettingsManager(tempPath);

        var settings = manager.Load();

        Assert.NotNull(settings);
        Assert.True(settings.IsAlwaysOnTop);
        Assert.False(settings.IsCompactMode);
        Assert.False(settings.StartWithWindows);
        Assert.Null(settings.WindowLeft);
        Assert.Null(settings.WindowTop);
        Assert.Null(settings.WidgetWidth);
    }

    [Fact]
    public void Load_ReturnsDefaultSettings_WhenJsonIsMalformed()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aiquotabar_test_{Guid.NewGuid():N}.json");
        File.WriteAllText(tempPath, "{ invalid json corrupt content !!! ");

        try
        {
            var manager = new SettingsManager(tempPath);
            var settings = manager.Load();

            Assert.NotNull(settings);
            Assert.True(settings.IsAlwaysOnTop);
            Assert.Null(settings.WidgetWidth);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void SaveAndLoad_RoundTripsAllPropertiesSuccessfully()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aiquotabar_test_{Guid.NewGuid():N}.json");

        try
        {
            var manager = new SettingsManager(tempPath);
            var original = new AppSettings
            {
                WindowLeft = 450.5,
                WindowTop = 120.0,
                WidgetWidth = 280.0,
                IsAlwaysOnTop = false,
                IsCompactMode = true,
                StartWithWindows = true
            };

            manager.Save(original);
            var loaded = manager.Load();

            Assert.Equal(450.5, loaded.WindowLeft);
            Assert.Equal(120.0, loaded.WindowTop);
            Assert.Equal(280.0, loaded.WidgetWidth);
            Assert.False(loaded.IsAlwaysOnTop);
            Assert.True(loaded.IsCompactMode);
            Assert.True(loaded.StartWithWindows);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void SettingsFileExists_ReturnsFalse_WhenFileDoesNotExist()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aiquotabar_test_{Guid.NewGuid():N}.json");
        var manager = new SettingsManager(tempPath);

        Assert.False(manager.SettingsFileExists);
    }

    [Fact]
    public void SettingsFileExists_ReturnsTrue_WhenFileExists()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aiquotabar_test_{Guid.NewGuid():N}.json");
        File.WriteAllText(tempPath, "{}");

        try
        {
            var manager = new SettingsManager(tempPath);
            Assert.True(manager.SettingsFileExists);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
