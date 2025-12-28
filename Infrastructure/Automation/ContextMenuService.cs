using ECommons.DalamudServices;
using static ECommons.GenericHelpers;

namespace AutoRetainerSellList.Infrastructure.Automation;

public class ContextMenuService
{
    public bool SelectMenuOption(string optionText, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        try
        {
            if (TryGetAddonMaster<ECommons.UIHelpers.AddonMasterImplementations.AddonMaster.ContextMenu>(out var contextMenu) && contextMenu.IsAddonReady)
            {
                var entries = contextMenu.Entries.ToList();

                foreach (var entry in entries)
                {
                    if (entry.Text.Contains(optionText, comparison) && entry.Enabled)
                    {
                        entry.Select();
                        return true;
                    }
                }

                Svc.Log.Warning($"[ContextMenuService] Menu option '{optionText}' not found or disabled");
                return false;
            }

            return false;
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[ContextMenuService] Error selecting menu option: {ex}");
            return false;
        }
    }

    public bool IsContextMenuOpen()
    {
        return TryGetAddonMaster<ECommons.UIHelpers.AddonMasterImplementations.AddonMaster.ContextMenu>(out var contextMenu) && contextMenu.IsAddonReady;
    }
}
