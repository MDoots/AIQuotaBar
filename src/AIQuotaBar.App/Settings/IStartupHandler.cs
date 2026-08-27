namespace AIQuotaBar.App.Settings;

public interface IStartupHandler
{
    Task<bool> IsStartupEnabledAsync();
    Task<bool> SetStartupAsync(bool enable);
}
