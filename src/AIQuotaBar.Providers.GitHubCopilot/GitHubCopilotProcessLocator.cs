namespace AIQuotaBar.Providers.GitHubCopilot;

public static class GitHubCopilotProcessLocator
{
    public static Func<string?>? CustomLocator { get; set; }

    public static string? LocateExecutable(
        Func<string, string?>? getEnvironmentVariable = null,
        Func<string, bool>? fileExists = null)
    {
        if (CustomLocator != null)
        {
            return CustomLocator();
        }

        getEnvironmentVariable ??= Environment.GetEnvironmentVariable;
        fileExists ??= File.Exists;

        var localAppData = getEnvironmentVariable("LOCALAPPDATA");
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            var wingetLink = Path.Combine(localAppData, "Microsoft", "WinGet", "Links", "copilot.exe");
            if (fileExists(wingetLink))
            {
                return wingetLink;
            }
        }

        var pathEnv = getEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathEnv))
        {
            var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var path in paths)
            {
                var candidate = Path.Combine(path, "copilot.exe");
                if (fileExists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
