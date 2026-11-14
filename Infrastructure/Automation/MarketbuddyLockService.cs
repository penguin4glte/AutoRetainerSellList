using Dalamud.Plugin.Services;
using ECommons.DalamudServices;

namespace AutoRetainerSellList.Infrastructure.Automation;

/// <summary>
/// Holds Marketbuddy's IPC lock while this plugin's automation is running.
///
/// Marketbuddy (https://github.com/PunishXIV/Marketbuddy) clicks "Compare Prices" automatically when the
/// RetainerSell window opens and also requests the price history. Combined with our own Compare Prices click,
/// that sends two market board requests for the same item: the second one resets the game's listing data and
/// its response can be delayed or never arrive, which led to wrong prices. While any lock is held, Marketbuddy
/// skips all of its automatic behavior.
///
/// The lock follows <see cref="TaskExecutor.IsBusy"/> (checked every frame) instead of explicit start/end calls,
/// so it is released however the automation ends (completion, abort, timeout, error), and on dispose.
/// Without Marketbuddy installed the IPC call fails and nothing happens.
/// </summary>
public sealed class MarketbuddyLockService : IDisposable
{
    private const string LockName = "AutoRetainerSellList";

    private readonly TaskExecutor _taskExecutor;
    private bool _locked;
    private bool _attemptedThisRun;

    public MarketbuddyLockService(TaskExecutor taskExecutor)
    {
        _taskExecutor = taskExecutor;
        Svc.Framework.Update += OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (_taskExecutor.IsBusy)
        {
            if (!_attemptedThisRun)
            {
                _attemptedThisRun = true;
                Lock();
            }
        }
        else
        {
            _attemptedThisRun = false;
            if (_locked)
                Unlock();
        }
    }

    private void Lock()
    {
        try
        {
            Svc.PluginInterface.GetIpcSubscriber<string, bool>("Marketbuddy.Lock").InvokeFunc(LockName);
            _locked = true;
            Svc.Log.Information("[MarketbuddyLock] Locked Marketbuddy for the duration of the automation");
        }
        catch (Exception ex)
        {
            // Marketbuddy is not installed / not loaded
            Svc.Log.Debug($"[MarketbuddyLock] Marketbuddy lock not acquired ({ex.GetType().Name})");
        }
    }

    private void Unlock()
    {
        try
        {
            Svc.PluginInterface.GetIpcSubscriber<string, bool>("Marketbuddy.Unlock").InvokeFunc(LockName);
            Svc.Log.Information("[MarketbuddyLock] Unlocked Marketbuddy");
        }
        catch (Exception ex)
        {
            Svc.Log.Warning($"[MarketbuddyLock] Failed to unlock Marketbuddy ({ex.GetType().Name}: {ex.Message})");
        }
        finally
        {
            _locked = false;
        }
    }

    public void Dispose()
    {
        Svc.Framework.Update -= OnFrameworkUpdate;
        if (_locked)
            Unlock();
    }
}
