namespace AIQuotaBar.App.Settings;

using Microsoft.Win32;

public sealed class RegistryStartupHandler : IStartupHandler
{
    public const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string AppName = "AIQuotaBar";

    private readonly Func<string, string?>? _getRegistryValue;
    private readonly Action<string, string>? _setRegistryValue;
    private readonly Action<string>? _deleteRegistryValue;

    public RegistryStartupHandler(
        Func<string, string?>? getRegistryValue = null,
        Action<string, string>? setRegistryValue = null,
        Action<string>? deleteRegistryValue = null)
    {
        _getRegistryValue = getRegistryValue;
        _setRegistryValue = setRegistryValue;
        _deleteRegistryValue = deleteRegistryValue;
    }

    public Task<bool> IsStartupEnabledAsync() => Task.FromResult(IsStartupEnabled());

    public bool IsStartupEnabled()
    {
        try
        {
            if (_getRegistryValue != null)
            {
                return !string.IsNullOrWhiteSpace(_getRegistryValue(AppName));
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

    public Task<bool> SetStartupAsync(bool enable) => Task.FromResult(SetStartup(enable));

    public bool SetStartup(bool enable, string? executablePath = null)
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

                if (_setRegistryValue != null)
                {
                    _setRegistryValue(AppName, quotedPath);
                    return true;
                }

                using var key = Registry.CurrentUser.CreateSubKey(RunRegistryKey, writable: true);
                key?.SetValue(AppName, quotedPath);
                return true;
            }
            else
            {
                if (_deleteRegistryValue != null)
                {
                    _deleteRegistryValue(AppName);
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
