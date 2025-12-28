using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoRetainerSellList.Infrastructure.GameClient;

public unsafe class InventoryService
{
    public (InventoryType inventoryType, uint slot, int quantity)? FindItemInRetainerInventory(uint itemId)
    {
        try
        {
            var inventoryManager = InventoryManager.Instance();
            if (inventoryManager == null)
            {
                Svc.Log.Warning("[InventoryService] InventoryManager is null");
                return null;
            }

            // Search through retainer inventory pages
            for (var page = InventoryType.RetainerPage1; page <= InventoryType.RetainerPage7; page++)
            {
                var inventory = inventoryManager->GetInventoryContainer(page);
                if (inventory == null) continue;

                for (var i = 0; i < inventory->Size; i++)
                {
                    var slot = inventory->Items[i];
                    if (slot.ItemId == itemId && slot.Quantity > 0)
                    {
                        return (page, (uint)i, (int)slot.Quantity);
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[InventoryService] Error finding item: {ex}");
            return null;
        }
    }

    public int GetCurrentMarketListingCount(uint itemId)
    {
        try
        {
            int count = 0;
            var inventoryManager = InventoryManager.Instance();
            if (inventoryManager == null)
            {
                Svc.Log.Warning("[InventoryService] InventoryManager is null");
                return 0;
            }

            var marketInventory = inventoryManager->GetInventoryContainer(InventoryType.RetainerMarket);
            if (marketInventory != null)
            {
                for (var i = 0; i < marketInventory->Size; i++)
                {
                    var slot = marketInventory->Items[i];
                    if (slot.ItemId == itemId)
                    {
                        count++;
                    }
                }
            }

            return count;
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[InventoryService] Error getting market listing count: {ex}");
            return 0;
        }
    }
}
