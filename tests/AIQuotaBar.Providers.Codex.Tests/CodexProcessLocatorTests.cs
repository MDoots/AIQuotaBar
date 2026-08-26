namespace AIQuotaBar.Providers.Codex.Tests;

using AIQuotaBar.Providers.Codex.Transport;
using Xunit;

public class CodexProcessLocatorTests
{
    [Fact]
    public void LocateExecutable_ReturnsOverride_WhenEnvironmentOverrideExists()
    {
        var env = new Dictionary<string, string>
        {
            [CodexProcessLocator.EnvironmentOverrideVariable] = @"C:\Custom\codex.exe",
            ["PATH"] = @"C:\Other"
        };
        var files = new HashSet<string> { @"C:\Custom\codex.exe" };

        var result = CodexProcessLocator.LocateExecutable(
            getEnvironmentVariable: key => env.GetValueOrDefault(key),
            fileExists: path => files.Contains(path));

        Assert.Equal(@"C:\Custom\codex.exe", result);
    }

    [Fact]
    public void LocateExecutable_ReturnsLocalAppDataCodex_WhenFoundInDesktopDir()
    {
        var env = new Dictionary<string, string>
        {
            ["LOCALAPPDATA"] = @"C:\Users\test\AppData\Local",
            ["PATH"] = @"C:\Windows"
        };
        var files = new HashSet<string>
        {
            @"C:\Users\test\AppData\Local\OpenAI\Codex\bin\v1\codex.exe"
        };

        var result = CodexProcessLocator.LocateExecutable(
            getEnvironmentVariable: key => env.GetValueOrDefault(key),
            fileExists: path => files.Contains(path),
            findFiles: (dir, pattern, opt) => dir.Contains("OpenAI") ? files.ToArray() : Array.Empty<string>());

        Assert.Equal(@"C:\Users\test\AppData\Local\OpenAI\Codex\bin\v1\codex.exe", result);
    }

    [Fact]
    public void LocateExecutable_ReturnsPathBinary_WhenOnPath()
    {
        var env = new Dictionary<string, string>
        {
            ["PATH"] = @"C:\Tools;C:\CodexBin"
        };
        var files = new HashSet<string>
        {
            @"C:\CodexBin\codex.exe"
        };

        var result = CodexProcessLocator.LocateExecutable(
            getEnvironmentVariable: key => env.GetValueOrDefault(key),
            fileExists: path => files.Contains(path));

        Assert.Equal(@"C:\CodexBin\codex.exe", result);
    }

    [Fact]
    public void LocateExecutable_ReturnsNpmVendorBinary_WhenInGlobalNpm()
    {
        var env = new Dictionary<string, string>
        {
            ["APPDATA"] = @"C:\Users\test\AppData\Roaming"
        };
        var npmVendor = @"C:\Users\test\AppData\Roaming\npm\node_modules\@openai\codex\node_modules\@openai\codex-win32-x64\vendor\x86_64-pc-windows-msvc\bin\codex.exe";
        var files = new HashSet<string> { npmVendor };

        var result = CodexProcessLocator.LocateExecutable(
            getEnvironmentVariable: key => env.GetValueOrDefault(key),
            fileExists: path => files.Contains(path));

        Assert.Equal(npmVendor, result);
    }

    [Fact]
    public void LocateExecutable_ReturnsNull_WhenNoExecutableFound()
    {
        var env = new Dictionary<string, string>
        {
            ["PATH"] = @"C:\Windows",
            ["LOCALAPPDATA"] = @"C:\Users\test\AppData\Local",
            ["APPDATA"] = @"C:\Users\test\AppData\Roaming"
        };

        var result = CodexProcessLocator.LocateExecutable(
            getEnvironmentVariable: key => env.GetValueOrDefault(key),
            fileExists: _ => false);

        Assert.Null(result);
    }
}
