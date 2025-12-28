using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Memory;

namespace AutoRetainerSellList.Services;

public class RetainerDataCache : IDisposable
{
    private List<RetainerInfo> cachedRetainers = new();
    private bool isValid = false;

    public class RetainerInfo
    {
        public ulong RetainerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
    }

    public List<RetainerInfo> GetRetainers()
    {
        if (!isValid)
        {
            UpdateCache();
        }
        return cachedRetainers;
    }

    public void UpdateCache()
    {
        cachedRetainers.Clear();

        try
        {
            unsafe
            {
                var manager = RetainerManager.Instance();
                if (manager == null)
                {
                    Svc.Log.Warning("RetainerManager not available");
                    isValid = false;
                    return;
                }

                for (var i = 0; i < manager->Retainers.Length; i++)
                {
                    var retainer = manager->Retainers[i];

                    // Valid retainer check (has ID, name, and not expired)
                    // Same check as AutoRetainer: ClassJob != 0 && Available
                    if (retainer.RetainerId != 0 &&
                        retainer.Name[0] != 0 &&
                        retainer.ClassJob != 0 &&
                        retainer.Available)
                    {
                        // Read SeString using Dalamud.Memory helper (same as AutoRetainer)
                        unsafe
                        {
                            fixed (byte* ptr = retainer.Name)
                            {
                                var seString = MemoryHelper.ReadSeStringNullTerminated((nint)ptr);
                                var name = seString?.TextValue ?? "";

                                if (string.IsNullOrEmpty(name))
                                {
                                    continue;
                                }

                                var displayOrder = -1;
                                for (var j = 0; j < manager->DisplayOrder.Length; j++)
                                {
                                    if (manager->DisplayOrder[j] == i)
                                    {
                                        displayOrder = j;
                                        break;
                                    }
                                }

                                cachedRetainers.Add(new RetainerInfo
                                {
                                    RetainerId = retainer.RetainerId,
                                    Name = name,
                                    DisplayOrder = displayOrder >= 0 ? displayOrder : i
                                });
                            }
                        }
                    }
                }

                // Sort by display order
                cachedRetainers = cachedRetainers.OrderBy(r => r.DisplayOrder).ToList();
                isValid = true;

                Svc.Log.Info($"RetainerDataCache updated: {cachedRetainers.Count} retainers found");
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Failed to update retainer cache: {ex}");
            cachedRetainers.Clear();
            isValid = false;
        }
    }

    public void Invalidate()
    {
        isValid = false;
    }

    public void Dispose()
    {
        cachedRetainers.Clear();
    }
}
