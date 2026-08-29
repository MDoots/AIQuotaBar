namespace AIQuotaBar.Providers.GrokBuild.Tests;

using AIQuotaBar.Providers.GrokBuild;
using Xunit;

public class GrokBuildProcessLocatorTests
{
    [Fact]
    public void LocateExecutable_WithCustomLocator_ReturnsCustomResult()
    {
        try
        {
            GrokBuildProcessLocator.CustomLocator = () => @"C:\Custom\grok.exe";
            var result = GrokBuildProcessLocator.LocateExecutable();
            Assert.Equal(@"C:\Custom\grok.exe", result);
        }
        finally
        {
            GrokBuildProcessLocator.CustomLocator = null;
        }
    }

    [Fact]
    public void LocateExecutable_FindsKnownUserProfileLocation()
    {
        var userProfile = @"C:\Users\testuser";
        var knownPath = Path.Combine(userProfile, ".grok", "bin", "grok.exe");

        var result = GrokBuildProcessLocator.LocateExecutable(
            getEnvironmentVariable: varName => varName == "USERPROFILE" ? userProfile : null,
            fileExists: path => path == knownPath);

        Assert.Equal(knownPath, result);
    }

    [Fact]
    public void LocateExecutable_FindsOnPath()
    {
        var pathEntry = @"C:\Tools";
        var candidate = Path.Combine(pathEntry, "grok.exe");

        var result = GrokBuildProcessLocator.LocateExecutable(
            getEnvironmentVariable: varName => varName == "PATH" ? pathEntry : null,
            fileExists: path => path == candidate);

        Assert.Equal(candidate, result);
    }

    [Fact]
    public void LocateExecutable_WhenNoneFound_ReturnsNull()
    {
        var result = GrokBuildProcessLocator.LocateExecutable(
            getEnvironmentVariable: _ => null,
            fileExists: _ => false);

        Assert.Null(result);
    }
}
