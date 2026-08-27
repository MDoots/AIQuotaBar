namespace AIQuotaBar.App.Settings;

using AIQuotaBar.App.Platform;

public static class StartupManager
{
    public const string RunRegistryKey = RegistryStartupHandler.RunRegistryKey;
    public const string AppName = RegistryStartupHandler.AppName;
    public const string StartupTaskId = PackagedStartupHandler.StartupTaskId;

    public static bool IsPackaged => PackageIdentity.IsPackaged;

    public static async Task<bool> IsStartupEnabledAsync(IStartupHandler? customHandler = null)
    {
        var handler = customHandler ?? GetDefaultHandler();
        return await handler.IsStartupEnabledAsync();
    }

    public static async Task<bool> SetStartupAsync(bool enable, IStartupHandler? customHandler = null)
    {
        var handler = customHandler ?? GetDefaultHandler();
        return await handler.SetStartupAsync(enable);
    }

    public static bool IsStartupEnabled(
        Func<string, string?>? getRegistryValue = null,
        IStartupHandler? customHandler = null)
    {
        if (customHandler != null)
        {
            return customHandler.IsStartupEnabledAsync().GetAwaiter().GetResult();
        }

        if (getRegistryValue != null)
        {
            return new RegistryStartupHandler(getRegistryValue: getRegistryValue).IsStartupEnabled();
        }

        if (IsPackaged)
        {
            return new PackagedStartupHandler().IsStartupEnabledAsync().GetAwaiter().GetResult();
        }

        return new RegistryStartupHandler().IsStartupEnabled();
    }

    public static bool SetStartup(
        bool enable,
        string? executablePath = null,
        Action<string, string>? setRegistryValue = null,
        Action<string>? deleteRegistryValue = null,
        IStartupHandler? customHandler = null)
    {
        if (customHandler != null)
        {
            return customHandler.SetStartupAsync(enable).GetAwaiter().GetResult();
        }

        if (setRegistryValue != null || deleteRegistryValue != null || executablePath != null)
        {
            return new RegistryStartupHandler(
                setRegistryValue: setRegistryValue,
                deleteRegistryValue: deleteRegistryValue).SetStartup(enable, executablePath);
        }

        if (IsPackaged)
        {
            return new PackagedStartupHandler().SetStartupAsync(enable).GetAwaiter().GetResult();
        }

        return new RegistryStartupHandler().SetStartup(enable, executablePath);
    }

    public static IStartupHandler GetDefaultHandler(IPackageIdentity? packageIdentity = null)
    {
        var isPackaged = packageIdentity?.IsPackaged ?? IsPackaged;
        return isPackaged
            ? new PackagedStartupHandler()
            : new RegistryStartupHandler();
    }
}
