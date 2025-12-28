using AutoRetainerSellList.Domain.Entities;
using AutoRetainerSellList.Domain.Repositories;
using AutoRetainerSellList.Domain.ValueObjects;
using Dalamud.Memory;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoRetainerSellList.Infrastructure.GameClient;

public unsafe class RetainerRepository : IRetainerRepository
{
    public Task<List<Retainer>> GetAllAvailableRetainersAsync()
    {
        var retainers = new List<Retainer>();

        try
        {
            var manager = RetainerManager.Instance();
            if (manager == null)
            {
                Svc.Log.Warning("[RetainerRepository] RetainerManager not available");
                return Task.FromResult(retainers);
            }

            for (var i = 0; i < manager->Retainers.Length; i++)
            {
                var retainer = manager->Retainers[i];

                // Valid retainer check
                if (retainer.RetainerId != 0 &&
                    retainer.Name[0] != 0 &&
                    retainer.ClassJob != 0 &&
                    retainer.Available)
                {
                    fixed (byte* ptr = retainer.Name)
                    {
                        var seString = MemoryHelper.ReadSeStringNullTerminated((nint)ptr);
                        var name = seString?.TextValue ?? "";

                        if (!string.IsNullOrEmpty(name))
                        {
                            var domainRetainer = new Retainer(
                                new RetainerId(retainer.RetainerId),
                                new RetainerName(name),
                                retainer.Available
                            );
                            retainers.Add(domainRetainer);
                        }
                    }
                }
            }

        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[RetainerRepository] Failed to get retainers: {ex}");
        }

        return Task.FromResult(retainers);
    }

    public Task<Retainer?> GetRetainerByIdAsync(RetainerId id)
    {
        try
        {
            var manager = RetainerManager.Instance();
            if (manager == null)
                return Task.FromResult<Retainer?>(null);

            for (var i = 0; i < manager->Retainers.Length; i++)
            {
                var retainer = manager->Retainers[i];

                if (retainer.RetainerId == id.Value && retainer.Available)
                {
                    fixed (byte* ptr = retainer.Name)
                    {
                        var seString = MemoryHelper.ReadSeStringNullTerminated((nint)ptr);
                        var name = seString?.TextValue ?? "";

                        if (!string.IsNullOrEmpty(name))
                        {
                            var domainRetainer = new Retainer(
                                new RetainerId(retainer.RetainerId),
                                new RetainerName(name),
                                retainer.Available
                            );
                            return Task.FromResult<Retainer?>(domainRetainer);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[RetainerRepository] Failed to get retainer {id}: {ex}");
        }

        return Task.FromResult<Retainer?>(null);
    }

    public Task<int> GetCurrentListedCountAsync(RetainerId retainerId, ItemId itemId)
    {
        try
        {
            int count = 0;
            var inventoryManager = InventoryManager.Instance();
            if (inventoryManager == null)
            {
                Svc.Log.Warning("[RetainerRepository] InventoryManager is null");
                return Task.FromResult(0);
            }

            // Check RetainerMarket inventory (items currently listed on the market board)
            var marketInventory = inventoryManager->GetInventoryContainer(InventoryType.RetainerMarket);
            if (marketInventory != null)
            {
                for (var i = 0; i < marketInventory->Size; i++)
                {
                    var slot = marketInventory->Items[i];
                    if (slot.ItemId == itemId.Value)
                    {
                        count++;
                    }
                }
            }

            return Task.FromResult(count);
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[RetainerRepository] Error getting listed count: {ex}");
            return Task.FromResult(0);
        }
    }

    public Task<InventorySlot?> FindItemInInventoryAsync(RetainerId retainerId, ItemId itemId)
    {
        try
        {
            var inventoryManager = InventoryManager.Instance();
            if (inventoryManager == null)
                return Task.FromResult<InventorySlot?>(null);

            // Search through retainer inventory pages
            for (var page = InventoryType.RetainerPage1; page <= InventoryType.RetainerPage7; page++)
            {
                var inventory = inventoryManager->GetInventoryContainer(page);
                if (inventory == null) continue;

                for (var i = 0; i < inventory->Size; i++)
                {
                    var slot = inventory->Items[i];
                    if (slot.ItemId == itemId.Value && slot.Quantity > 0)
                    {
                        return Task.FromResult<InventorySlot?>(new InventorySlot(
                            page.ToString(),
                            (uint)i,
                            (int)slot.Quantity
                        ));
                    }
                }
            }

        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[RetainerRepository] Error finding item in inventory: {ex}");
        }

        return Task.FromResult<InventorySlot?>(null);
    }
}
