namespace AIQuotaBar.App.Settings;

using Microsoft.Win32;

public static class StartupManager
{
    public const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string AppName = "AIQuotaBar";

    public static bool IsStartupEnabled(Func<string, string?>? getRegistryValue = null)
    {
        try
        {
            if (getRegistryValue != null)
            {
                return !string.IsNullOrWhiteSpace(getRegistryValue(AppName));
            }

            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, writable: false);
            var value = key?.GetValue(AppName) as string;
            return !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }

    public static bool SetStartup(
        bool enable,
        string? executablePath = null,
        Action<string, string>? setRegistryValue = null,
        Action<string>? deleteRegistryValue = null)
    {
        try
        {
            if (enable)
            {
                executablePath ??= Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(executablePath) || !executablePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var quotedPath = $"\"{executablePath}\"";

                if (setRegistryValue != null)
                {
                    setRegistryValue(AppName, quotedPath);
                    return true;
                }

                using var key = Registry.CurrentUser.CreateSubKey(RunRegistryKey, writable: true);
                key?.SetValue(AppName, quotedPath);
                return true;
            }
            else
            {
                if (deleteRegistryValue != null)
                {
                    deleteRegistryValue(AppName);
                    return true;
                }

                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, writable: true);
                key?.DeleteValue(AppName, throwOnMissingValue: false);
                return true;
            }
        }
        catch
        {
            return false;
        }
    }
}
