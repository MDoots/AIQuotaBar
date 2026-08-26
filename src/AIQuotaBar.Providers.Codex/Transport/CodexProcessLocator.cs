namespace AIQuotaBar.Providers.Codex.Transport;

public static class CodexProcessLocator
{
    public const string EnvironmentOverrideVariable = "AIQUOTABAR_CODEX_PATH";

    public static string? LocateExecutable(
        Func<string, string?>? getEnvironmentVariable = null,
        Func<string, bool>? fileExists = null,
        Func<string, string, SearchOption, string[]>? findFiles = null)
    {
        getEnvironmentVariable ??= Environment.GetEnvironmentVariable;
        fileExists ??= File.Exists;
        findFiles ??= (dir, pattern, option) => Directory.Exists(dir)
            ? Directory.GetFiles(dir, pattern, option)
            : Array.Empty<string>();

        // 1. Explicit AIQuotaBar override
        var customOverride = getEnvironmentVariable(EnvironmentOverrideVariable);
        if (!string.IsNullOrWhiteSpace(customOverride) && fileExists(customOverride))
        {
            return customOverride;
        }

        var localAppData = getEnvironmentVariable("LOCALAPPDATA");
        var appData = getEnvironmentVariable("APPDATA");
        var programFiles = getEnvironmentVariable("ProgramFiles");

        // 2. Known native Codex Desktop per-user executable locations
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            var openAiCodexDir = Path.Combine(localAppData, "OpenAI", "Codex");
            if (Directory.Exists(openAiCodexDir) || findFiles != null)
            {
                var matches = findFiles(openAiCodexDir, "codex.exe", SearchOption.AllDirectories);
                var candidate = matches.FirstOrDefault(fileExists);
                if (!string.IsNullOrEmpty(candidate))
                {
                    return candidate;
                }
            }

            var programsCodex = Path.Combine(localAppData, "Programs", "Codex", "codex.exe");
            if (fileExists(programsCodex))
            {
                return programsCodex;
            }

            var programsOpenAiCodex = Path.Combine(localAppData, "Programs", "OpenAI Codex", "codex.exe");
            if (fileExists(programsOpenAiCodex))
            {
                return programsOpenAiCodex;
            }
        }

        // 3. Native codex.exe available through PATH
        var pathVar = getEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathVar))
        {
            var pathEntries = pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var pathEntry in pathEntries)
            {
                var candidate = Path.Combine(pathEntry, "codex.exe");
                if (fileExists(candidate))
                {
                    return candidate;
                }
            }
        }

        // 4. npm/global Codex installations, resolving to the native Windows Codex binary
        if (!string.IsNullOrWhiteSpace(appData))
        {
            var npmVendorBinary = Path.Combine(
                appData,
                "npm",
                "node_modules",
                "@openai",
                "codex",
                "node_modules",
                "@openai",
                "codex-win32-x64",
                "vendor",
                "x86_64-pc-windows-msvc",
                "bin",
                "codex.exe");

            if (fileExists(npmVendorBinary))
            {
                return npmVendorBinary;
            }
        }

        // 5. If codex.cmd is on PATH, attempt to resolve sibling node_modules to find native codex.exe
        if (!string.IsNullOrWhiteSpace(pathVar))
        {
            var pathEntries = pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var pathEntry in pathEntries)
            {
                var candidateCmd = Path.Combine(pathEntry, "codex.cmd");
                if (fileExists(candidateCmd))
                {
                    var resolvedVendor = Path.Combine(
                        pathEntry,
                        "node_modules",
                        "@openai",
                        "codex",
                        "node_modules",
                        "@openai",
                        "codex-win32-x64",
                        "vendor",
                        "x86_64-pc-windows-msvc",
                        "bin",
                        "codex.exe");

                    if (fileExists(resolvedVendor))
                    {
                        return resolvedVendor;
                    }
                }
            }
        }

        // 6. Native binary not found (do not return .cmd batch wrappers)
        return null;
    }
}
