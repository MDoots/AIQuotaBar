namespace AIQuotaBar.Providers.Antigravity.Transport;

public static class AntigravityProcessLocator
{
    public const string EnvironmentOverrideVariable = "AIQUOTABAR_ANTIGRAVITY_PATH";
    public const string EnvironmentOverrideVariableShort = "AIQUOTABAR_AGY_PATH";

    public static string? LocateExecutable(
        Func<string, string?>? getEnvironmentVariable = null,
        Func<string, bool>? fileExists = null)
    {
        getEnvironmentVariable ??= Environment.GetEnvironmentVariable;
        fileExists ??= File.Exists;

        // 1. Explicit AIQuotaBar environment overrides
        var customOverride = getEnvironmentVariable(EnvironmentOverrideVariable);
        if (!string.IsNullOrWhiteSpace(customOverride) && fileExists(customOverride))
        {
            return customOverride;
        }

        var shortOverride = getEnvironmentVariable(EnvironmentOverrideVariableShort);
        if (!string.IsNullOrWhiteSpace(shortOverride) && fileExists(shortOverride))
        {
            return shortOverride;
        }

        var localAppData = getEnvironmentVariable("LOCALAPPDATA");
        var appData = getEnvironmentVariable("APPDATA");
        var userProfile = getEnvironmentVariable("USERPROFILE");
        var programFiles = getEnvironmentVariable("ProgramFiles");

        // 2. Official default Windows install path: %LOCALAPPDATA%\agy\bin\agy.exe
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            var defaultAgyPath = Path.Combine(localAppData, "agy", "bin", "agy.exe");
            if (fileExists(defaultAgyPath))
            {
                return defaultAgyPath;
            }
        }

        // 3. Search PATH for agy.exe
        var pathVar = getEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathVar))
        {
            var pathEntries = pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var pathEntry in pathEntries)
            {
                var candidate = Path.Combine(pathEntry, "agy.exe");
                if (fileExists(candidate))
                {
                    return candidate;
                }
            }
        }

        // 4. Secondary fallback locations
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            var geminiBin = Path.Combine(userProfile, ".gemini", "antigravity", "bin", "agy.exe");
            if (fileExists(geminiBin))
            {
                return geminiBin;
            }
        }

        if (!string.IsNullOrWhiteSpace(appData))
        {
            var roamingBin = Path.Combine(appData, "Antigravity", "bin", "agy.exe");
            if (fileExists(roamingBin))
            {
                return roamingBin;
            }
        }

        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            var progFilesBin = Path.Combine(programFiles, "Antigravity", "bin", "agy.exe");
            if (fileExists(progFilesBin))
            {
                return progFilesBin;
            }
        }

        return null;
    }
}
