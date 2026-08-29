namespace AIQuotaBar.Providers.GitHubCopilot.Tests;

using AIQuotaBar.Providers.GitHubCopilot;
using Xunit;

public class GitHubCopilotProcessLocatorTests
{
    [Fact]
    public void LocateExecutable_WithCustomLocator_ReturnsCustomResult()
    {
        try
        {
            GitHubCopilotProcessLocator.CustomLocator = () => @"C:\Custom\copilot.exe";
            var result = GitHubCopilotProcessLocator.LocateExecutable();
            Assert.Equal(@"C:\Custom\copilot.exe", result);
        }
        finally
        {
            GitHubCopilotProcessLocator.CustomLocator = null;
        }
    }

    [Fact]
    public void LocateExecutable_FindsWinGetLink()
    {
        var localAppData = @"C:\Users\testuser\AppData\Local";
        var wingetLink = Path.Combine(localAppData, "Microsoft", "WinGet", "Links", "copilot.exe");

        var result = GitHubCopilotProcessLocator.LocateExecutable(
            getEnvironmentVariable: varName => varName == "LOCALAPPDATA" ? localAppData : null,
            fileExists: path => path == wingetLink);

        Assert.Equal(wingetLink, result);
    }

    [Fact]
    public void LocateExecutable_FindsOnPath()
    {
        var pathEntry = @"C:\Tools";
        var candidate = Path.Combine(pathEntry, "copilot.exe");

        var result = GitHubCopilotProcessLocator.LocateExecutable(
            getEnvironmentVariable: varName => varName == "PATH" ? pathEntry : null,
            fileExists: path => path == candidate);

        Assert.Equal(candidate, result);
    }

    [Fact]
    public void LocateExecutable_WhenNoneFound_ReturnsNull()
    {
        var result = GitHubCopilotProcessLocator.LocateExecutable(
            getEnvironmentVariable: _ => null,
            fileExists: _ => false);

        Assert.Null(result);
    }
}
