using ECommons.Automation.LegacyTaskManager;
using ECommons.DalamudServices;

namespace AutoRetainerSellList.Infrastructure.Automation;

public class TaskExecutor : IDisposable
{
    private readonly TaskManager _taskManager;

    public bool IsBusy => _taskManager.IsBusy;

    public TaskExecutor()
    {
        _taskManager = new TaskManager
        {
            TimeLimitMS = 300000, // 5 minutes
            AbortOnTimeout = false
        };
    }

    public void Enqueue(Func<bool?> task, string? taskName = null)
    {
        _taskManager.Enqueue(task, taskName);
    }

    public void EnqueueDelay(int milliseconds)
    {
        _taskManager.DelayNext(milliseconds);
    }

    public void Abort()
    {
        _taskManager.Abort();
    }

    public void SetTimeout(int milliseconds)
    {
        _taskManager.TimeLimitMS = milliseconds;
    }

    public void Dispose()
    {
        _taskManager?.Abort();
    }
}
