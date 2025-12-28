using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using static ECommons.GenericHelpers;

namespace AutoRetainerSellList.Infrastructure.Automation;

public unsafe class AddonInteractionService
{
    public bool WaitForAddon(string addonName)
    {
        if (TryGetAddonByName<AtkUnitBase>(addonName, out var addon) && IsAddonReady(addon))
        {
            return true;
        }

        return false;
    }

    public bool CloseAddon(string addonName)
    {
        try
        {
            if (TryGetAddonByName<AtkUnitBase>(addonName, out var addon))
            {
                addon->Close(true);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[AddonInteractionService] Error closing addon {addonName}: {ex}");
            return false;
        }
    }

    public bool IsAddonOpen(string addonName)
    {
        return TryGetAddonByName<AtkUnitBase>(addonName, out var addon) && IsAddonReady(addon);
    }

    public bool FireCallback(string addonName, bool updateState, params object[] parameters)
    {
        try
        {
            if (TryGetAddonByName<AtkUnitBase>(addonName, out var addon) && IsAddonReady(addon))
            {
                // Build parameter list with updateState first, then params
                var allParams = new List<object> { updateState };
                if (parameters != null)
                    allParams.AddRange(parameters);

                ECommons.Automation.Callback.Fire(addon, updateState, parameters);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[AddonInteractionService] Error firing callback on {addonName}: {ex}");
            return false;
        }
    }
}
