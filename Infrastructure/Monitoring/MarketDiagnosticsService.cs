using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Network.Structures;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoRetainerSellList.Infrastructure.Monitoring;

/// <summary>
/// Diagnostic logging for investigating conflicts with other market board plugins (e.g. Marketbuddy).
/// All lines are prefixed with "[MBDiag]" and logged at Debug level.
///
/// - Addon lifecycle of RetainerSell / ItemSearchResult / ItemHistory, including ReceiveEvent. Other plugins that
///   "click" buttons via ReceiveEvent (Marketbuddy's auto Compare Prices = event param 4 on RetainerSell) show up
///   here. Our own clicks use Callback.Fire and do NOT appear as ReceiveEvent.
/// - Market board history packets (the game requests the history together with each Compare Prices request).
/// - Marketbuddy install / load / IPC lock state.
/// </summary>
public sealed class MarketDiagnosticsService : IDisposable
{
    private static readonly string[] WatchedAddons = ["RetainerSell", "ItemSearchResult", "ItemHistory"];

    public MarketDiagnosticsService()
    {
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, WatchedAddons, OnAddonEvent);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, WatchedAddons, OnAddonEvent);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PreReceiveEvent, WatchedAddons, OnAddonEvent);
        Svc.MarketBoard.HistoryReceived += OnHistoryReceived;

        Log($"Diagnostics started. {DescribeMarketbuddy()}");
    }

    // Debug level: enable debug logging in Dalamud to see these when investigating a report
    public static void Log(string message) => Svc.Log.Debug($"[MBDiag] {message}");

    /// <summary>Describes whether Marketbuddy is installed/loaded and whether its IPC lock is held.</summary>
    public static string DescribeMarketbuddy()
    {
        try
        {
            var plugins = Svc.PluginInterface.InstalledPlugins
                .Where(p => p.InternalName.Contains("Marketbuddy", StringComparison.OrdinalIgnoreCase)
                            || p.Name.Contains("Marketbuddy", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (plugins.Count == 0)
                return "Marketbuddy: not installed";

            var desc = string.Join(", ", plugins.Select(p =>
                $"{p.Name} ({p.InternalName}) v{p.Version} loaded={p.IsLoaded} testing={p.IsTesting} dev={p.IsDev}"));

            string lockState;
            try
            {
                var locked = Svc.PluginInterface.GetIpcSubscriber<string, bool>("Marketbuddy.IsLocked").InvokeFunc(null!);
                lockState = locked ? "locked" : "not locked";
            }
            catch (Exception ex)
            {
                lockState = $"IPC unavailable ({ex.GetType().Name})";
            }

            return $"Marketbuddy: {desc}, IPC lock: {lockState}";
        }
        catch (Exception ex)
        {
            return $"Marketbuddy: check failed ({ex.GetType().Name}: {ex.Message})";
        }
    }

    private void OnAddonEvent(AddonEvent type, AddonArgs args)
    {
        try
        {
            if (args is AddonReceiveEventArgs re)
            {
                // Mouse movement noise is skipped; clicks/changes are what matter here
                var eventType = (AtkEventType)re.AtkEventType;
                if (eventType is AtkEventType.MouseMove or AtkEventType.MouseOver or AtkEventType.MouseOut
                    or AtkEventType.MouseWheel or AtkEventType.FocusStart or AtkEventType.FocusStop)
                    return;
                Log($"{args.AddonName} ReceiveEvent: eventType={eventType}({(int)eventType}), param={re.EventParam} " +
                    "(not from this plugin: our clicks use Callback.Fire)");
            }
            else
            {
                Log($"{args.AddonName} {type}");
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[MBDiag] Error in addon listener: {ex}");
        }
    }

    private void OnHistoryReceived(IMarketBoardHistory history)
    {
        try
        {
            Log($"Market board HISTORY received: item {history.ItemId}, {history.HistoryListings.Count} entries " +
                "(the game requests the history together with Compare Prices)");
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[MBDiag] Error in history listener: {ex}");
        }
    }

    public void Dispose()
    {
        Svc.AddonLifecycle.UnregisterListener(OnAddonEvent);
        Svc.MarketBoard.HistoryReceived -= OnHistoryReceived;
    }
}
