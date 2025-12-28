using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using ECommons.DalamudServices;
using System;

namespace AutoRetainerSellList.Services;

/// <summary>
/// Debug service to log all addon events to help identify the correct addon name
/// </summary>
public class AddonDebugger : IDisposable
{
    private bool isEnabled = false;

    public AddonDebugger(bool enableOnStart = false)
    {
        isEnabled = enableOnStart;
        if (isEnabled)
        {
            Enable();
        }
    }

    public void Enable()
    {
        if (isEnabled) return;

        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, OnAnyAddonSetup);
        isEnabled = true;
        Svc.Log.Info("[AddonDebugger] Enabled - will log all addon PostSetup events");
    }

    public void Disable()
    {
        if (!isEnabled) return;

        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, OnAnyAddonSetup);
        isEnabled = false;
        Svc.Log.Info("[AddonDebugger] Disabled");
    }

    private void OnAnyAddonSetup(AddonEvent type, AddonArgs args)
    {
        try
        {
            var addonName = args.AddonName;

            // Only log retainer-related addons
            if (addonName.Contains("Retainer", StringComparison.OrdinalIgnoreCase) ||
                addonName.Contains("Sell", StringComparison.OrdinalIgnoreCase))
            {
                Svc.Log.Info($"[AddonDebugger] Addon opened: {addonName}");
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[AddonDebugger] Error: {ex}");
        }
    }

    public void Dispose()
    {
        Disable();
    }
}
