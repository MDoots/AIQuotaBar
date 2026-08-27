namespace AIQuotaBar.App.Settings;

using Windows.ApplicationModel;

public interface IStartupTaskProxy
{
    StartupTaskState State { get; }
    Task<StartupTaskState> RequestEnableAsync();
    void Disable();
}

internal sealed class WindowsStartupTaskProxy : IStartupTaskProxy
{
    private readonly StartupTask _task;

    public WindowsStartupTaskProxy(StartupTask task)
    {
        _task = task;
    }

    public StartupTaskState State => _task.State;

    public async Task<StartupTaskState> RequestEnableAsync()
    {
        return await _task.RequestEnableAsync();
    }

    public void Disable()
    {
        _task.Disable();
    }
}

public sealed class PackagedStartupHandler : IStartupHandler
{
    public const string StartupTaskId = "AIQuotaBarStartup";
    private readonly Func<string, Task<IStartupTaskProxy?>> _taskProvider;

    public PackagedStartupHandler(Func<string, Task<IStartupTaskProxy?>>? taskProvider = null)
    {
        _taskProvider = taskProvider ?? GetRealStartupTaskAsync;
    }

    public async Task<bool> IsStartupEnabledAsync()
    {
        try
        {
            var task = await _taskProvider(StartupTaskId);
            if (task == null)
            {
                return false;
            }

            return task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SetStartupAsync(bool enable)
    {
        try
        {
            var task = await _taskProvider(StartupTaskId);
            if (task == null)
            {
                return false;
            }

            if (enable)
            {
                switch (task.State)
                {
                    case StartupTaskState.Enabled:
                    case StartupTaskState.EnabledByPolicy:
                        return true;

                    case StartupTaskState.DisabledByUser:
                    case StartupTaskState.DisabledByPolicy:
                        return false;

                    case StartupTaskState.Disabled:
                        var result = await task.RequestEnableAsync();
                        return result is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;

                    default:
                        return false;
                }
            }
            else
            {
                if (task.State is StartupTaskState.EnabledByPolicy)
                {
                    return false;
                }

                if (task.State is StartupTaskState.Disabled or StartupTaskState.DisabledByUser or StartupTaskState.DisabledByPolicy)
                {
                    return true;
                }

                task.Disable();
                return task.State is StartupTaskState.Disabled or StartupTaskState.DisabledByUser or StartupTaskState.DisabledByPolicy;
            }
        }
        catch
        {
            return false;
        }
    }

    private static async Task<IStartupTaskProxy?> GetRealStartupTaskAsync(string taskId)
    {
        try
        {
            var task = await StartupTask.GetAsync(taskId);
            return task != null ? new WindowsStartupTaskProxy(task) : null;
        }
        catch
        {
            return null;
        }
    }
}
