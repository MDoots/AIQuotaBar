namespace AIQuotaBar.App.Tests;

using AIQuotaBar.App.Settings;
using Xunit;

public class StartupManagerTests
{
    [Fact]
    public void SetStartup_SetsQuotedPath_WhenEnabled()
    {
        string? storedKey = null;
        string? storedValue = null;

        var success = StartupManager.SetStartup(
            enable: true,
            executablePath: @"C:\Program Files\AIQuotaBar\AIQuotaBar.App.exe",
            setRegistryValue: (key, val) =>
            {
                storedKey = key;
                storedValue = val;
            });

        Assert.True(success);
        Assert.Equal("AIQuotaBar", storedKey);
        Assert.Equal("\"C:\\Program Files\\AIQuotaBar\\AIQuotaBar.App.exe\"", storedValue);
    }

    [Fact]
    public void SetStartup_DeletesEntry_WhenDisabled()
    {
        string? deletedKey = null;

        var success = StartupManager.SetStartup(
            enable: false,
            deleteRegistryValue: key => deletedKey = key);

        Assert.True(success);
        Assert.Equal("AIQuotaBar", deletedKey);
    }

    [Fact]
    public void SetStartup_FailsGracefully_WhenPathIsNotExe()
    {
        var success = StartupManager.SetStartup(
            enable: true,
            executablePath: @"C:\some\script.bat");

        Assert.False(success);
    }

    [Fact]
    public void IsStartupEnabled_ReturnsTrue_WhenValueIsPresent()
    {
        var isEnabled = StartupManager.IsStartupEnabled(
            getRegistryValue: key => key == "AIQuotaBar" ? "\"C:\\app.exe\"" : null);

        Assert.True(isEnabled);
    }

    [Fact]
    public void IsStartupEnabled_ReturnsFalse_WhenValueIsEmptyOrNull()
    {
        var isEnabled = StartupManager.IsStartupEnabled(getRegistryValue: _ => null);
        Assert.False(isEnabled);
    }
}
