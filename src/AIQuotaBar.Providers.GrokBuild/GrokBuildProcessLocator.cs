namespace AIQuotaBar.Providers.GrokBuild;

public static class GrokBuildProcessLocator
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

        var userProfile = getEnvironmentVariable("USERPROFILE");
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            var knownPath = Path.Combine(userProfile, ".grok", "bin", "grok.exe");
            if (fileExists(knownPath))
            {
                return knownPath;
            }
        }

        var pathEnv = getEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathEnv))
        {
            var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var path in paths)
            {
                var candidate = Path.Combine(path, "grok.exe");
                if (fileExists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
