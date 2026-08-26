namespace AIQuotaBar.Providers.Antigravity.Tests;

using AIQuotaBar.Providers.Antigravity.Transport;
using Xunit;

public class AntigravityProcessLocatorTests
{
    [Fact]
    public void LocateExecutable_ReturnsCustomOverride_WhenEnvironmentVariableSet()
    {
        var customPath = @"C:\CustomTools\agy.exe";
        var envVars = new Dictionary<string, string>
        {
            [AntigravityProcessLocator.EnvironmentOverrideVariable] = customPath
        };

        var result = AntigravityProcessLocator.LocateExecutable(
            getEnvironmentVariable: key => envVars.GetValueOrDefault(key),
            fileExists: path => path == customPath);

        Assert.Equal(customPath, result);
    }

    [Fact]
    public void LocateExecutable_ReturnsShortCustomOverride_WhenSet()
    {
        var customPath = @"C:\CustomTools\agy.exe";
        var envVars = new Dictionary<string, string>
        {
            [AntigravityProcessLocator.EnvironmentOverrideVariableShort] = customPath
        };

        var result = AntigravityProcessLocator.LocateExecutable(
            getEnvironmentVariable: key => envVars.GetValueOrDefault(key),
            fileExists: path => path == customPath);

        Assert.Equal(customPath, result);
    }

    [Fact]
    public void LocateExecutable_ReturnsDefaultLocalAppDataPath_WhenPresent()
    {
        var localAppData = @"C:\Users\testuser\AppData\Local";
        var expectedPath = Path.Combine(localAppData, "agy", "bin", "agy.exe");

        var envVars = new Dictionary<string, string>
        {
            ["LOCALAPPDATA"] = localAppData
        };

        var result = AntigravityProcessLocator.LocateExecutable(
            getEnvironmentVariable: key => envVars.GetValueOrDefault(key),
            fileExists: path => path == expectedPath);

        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void LocateExecutable_ReturnsPathFromEnvironmentPath_WhenFoundInPath()
    {
        var pathDir = @"C:\Tools\Bin";
        var expectedPath = Path.Combine(pathDir, "agy.exe");

        var envVars = new Dictionary<string, string>
        {
            ["PATH"] = $@"C:\Windows;{pathDir};C:\Other"
        };

        var result = AntigravityProcessLocator.LocateExecutable(
            getEnvironmentVariable: key => envVars.GetValueOrDefault(key),
            fileExists: path => path == expectedPath);

        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void LocateExecutable_ReturnsNull_WhenNotInstalled()
    {
        var result = AntigravityProcessLocator.LocateExecutable(
            getEnvironmentVariable: _ => null,
            fileExists: _ => false);

        Assert.Null(result);
    }
}
