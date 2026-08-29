namespace AIQuotaBar.Providers.ClaudeCode.Tests;

using AIQuotaBar.Providers.ClaudeCode;
using Xunit;

public class ClaudeCodeProcessLocatorTests
{
    [Fact]
    public void LocateExecutable_WithCustomLocator_ReturnsCustomResult()
    {
        try
        {
            ClaudeCodeProcessLocator.CustomLocator = () => @"C:\Custom\claude.exe";
            var result = ClaudeCodeProcessLocator.LocateExecutable();
            Assert.Equal(@"C:\Custom\claude.exe", result);
        }
        finally
        {
            ClaudeCodeProcessLocator.CustomLocator = null;
        }
    }

    [Fact]
    public void LocateExecutable_PrefersNativeDotLocalBin()
    {
        var userProfile = @"C:\Users\testuser";
        var nativePath = Path.Combine(userProfile, ".local", "bin", "claude.exe");

        var result = ClaudeCodeProcessLocator.LocateExecutable(
            getEnvironmentVariable: varName => varName == "USERPROFILE" ? userProfile : null,
            fileExists: path => path == nativePath);

        Assert.Equal(nativePath, result);
    }

    [Fact]
    public void LocateExecutable_FindsNativeOnPath()
    {
        var pathEntry = @"C:\Tools";
        var nativePath = Path.Combine(pathEntry, "claude.exe");

        var result = ClaudeCodeProcessLocator.LocateExecutable(
            getEnvironmentVariable: varName => varName == "PATH" ? pathEntry : null,
            fileExists: path => path == nativePath);

        Assert.Equal(nativePath, result);
    }

    [Fact]
    public void LocateExecutable_ResolvesNativeFromNpmPackage()
    {
        var appData = @"C:\Users\testuser\AppData\Roaming";
        var nativeNpmPath = Path.Combine(appData, "npm", "node_modules", "@anthropic-ai", "claude-code-win32-x64", "claude.exe");

        var result = ClaudeCodeProcessLocator.LocateExecutable(
            getEnvironmentVariable: varName => varName == "APPDATA" ? appData : null,
            fileExists: path => path == nativeNpmPath);

        Assert.Equal(nativeNpmPath, result);
    }

    [Fact]
    public void LocateExecutable_ResolvesNativeFromCmdSiblingPath()
    {
        var pathEntry = @"C:\npm";
        var candidateCmd = Path.Combine(pathEntry, "claude.cmd");
        var resolvedNative = Path.Combine(pathEntry, "node_modules", "@anthropic-ai", "claude-code-win32-x64", "claude.exe");

        var result = ClaudeCodeProcessLocator.LocateExecutable(
            getEnvironmentVariable: varName => varName == "PATH" ? pathEntry : null,
            fileExists: path => path == candidateCmd || path == resolvedNative);

        Assert.Equal(resolvedNative, result);
    }

    [Fact]
    public void LocateExecutable_WhenOnlyCmdWrapperExistsWithoutNativeBinary_ReturnsNull()
    {
        var pathEntry = @"C:\npm";
        var candidateCmd = Path.Combine(pathEntry, "claude.cmd");

        var result = ClaudeCodeProcessLocator.LocateExecutable(
            getEnvironmentVariable: varName => varName == "PATH" ? pathEntry : null,
            fileExists: path => path == candidateCmd);

        Assert.Null(result);
    }

    [Fact]
    public void LocateExecutable_WhenNoneFound_ReturnsNull()
    {
        var result = ClaudeCodeProcessLocator.LocateExecutable(
            getEnvironmentVariable: _ => null,
            fileExists: _ => false);

        Assert.Null(result);
    }
}
