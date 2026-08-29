namespace AIQuotaBar.Providers.ClaudeCode;

public static class ClaudeCodeProcessLocator
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
        var localAppData = getEnvironmentVariable("LOCALAPPDATA");
        var appData = getEnvironmentVariable("APPDATA");
        var pathVar = getEnvironmentVariable("PATH");

        // 1. Official native binary location (%USERPROFILE%\.local\bin\claude.exe)
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            var nativeLocalBin = Path.Combine(userProfile, ".local", "bin", "claude.exe");
            if (fileExists(nativeLocalBin))
            {
                return nativeLocalBin;
            }

            var dotClaudeBin = Path.Combine(userProfile, ".claude", "bin", "claude.exe");
            if (fileExists(dotClaudeBin))
            {
                return dotClaudeBin;
            }
        }

        // 2. Native claude.exe on PATH
        if (!string.IsNullOrWhiteSpace(pathVar))
        {
            var pathEntries = pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var pathEntry in pathEntries)
            {
                var candidateExe = Path.Combine(pathEntry, "claude.exe");
                if (fileExists(candidateExe))
                {
                    return candidateExe;
                }
            }
        }

        // 3. Native binary inside npm global modules
        if (!string.IsNullOrWhiteSpace(appData))
        {
            var npmNative1 = Path.Combine(appData, "npm", "node_modules", "@anthropic-ai", "claude-code-win32-x64", "claude.exe");
            if (fileExists(npmNative1))
            {
                return npmNative1;
            }

            var npmNative2 = Path.Combine(appData, "npm", "node_modules", "@anthropic-ai", "claude-code", "bin", "claude.exe");
            if (fileExists(npmNative2))
            {
                return npmNative2;
            }
        }

        // 4. PATH wrapper resolution to native binary
        if (!string.IsNullOrWhiteSpace(pathVar))
        {
            var pathEntries = pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var pathEntry in pathEntries)
            {
                var candidateCmd = Path.Combine(pathEntry, "claude.cmd");
                if (fileExists(candidateCmd))
                {
                    var resolvedNative = Path.Combine(pathEntry, "node_modules", "@anthropic-ai", "claude-code-win32-x64", "claude.exe");
                    if (fileExists(resolvedNative))
                    {
                        return resolvedNative;
                    }

                    var resolvedNative2 = Path.Combine(pathEntry, "node_modules", "@anthropic-ai", "claude-code", "bin", "claude.exe");
                    if (fileExists(resolvedNative2))
                    {
                        return resolvedNative2;
                    }
                }
            }
        }

        return null;
    }
}
