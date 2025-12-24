using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using System;
using System.Numerics;

namespace RetainerPriceAdjuster.Services;

public class BellProximityService : IDisposable
{
    private readonly Plugin plugin;
    private bool wasNearBell = false;

    public bool IsNearBell { get; private set; } = false;
    public float DistanceToBell { get; private set; } = float.MaxValue;
    public IGameObject? NearestBell { get; private set; } = null;

    // Known retainer bell object data IDs
    private readonly uint[] RetainerBellDataIds = new uint[]
    {
        2000401, // Limsa Lominsa
        2000403, // Gridania
        2000404, // Ul'dah
        2000441, // Ishgard
        2000837, // Kugane
        2001271, // Crystarium
        2001978, // Old Sharlayan
        2007513, // Tuliyollal
    };

    public BellProximityService(Plugin plugin)
    {
        this.plugin = plugin;
    }

    public void Update()
    {
        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        if (localPlayer == null)
        {
            IsNearBell = false;
            DistanceToBell = float.MaxValue;
            return;
        }

        var playerPos = localPlayer.Position;
        var nearestBellDistance = float.MaxValue;
        IGameObject? nearestBellObj = null;

        // Check all objects for retainer bells
        foreach (var obj in Plugin.ObjectTable)
        {
            if (obj == null || obj.ObjectKind != ObjectKind.EventObj)
                continue;

            // Check if this is a retainer bell
            bool isBell = false;
            foreach (var bellId in RetainerBellDataIds)
            {
                if (obj.BaseId == bellId)
                {
                    isBell = true;
                    break;
                }
            }

            if (!isBell)
                continue;

            var distance = Vector3.Distance(playerPos, obj.Position);
            if (distance < nearestBellDistance)
            {
                nearestBellDistance = distance;
                nearestBellObj = obj;
            }
        }

        DistanceToBell = nearestBellDistance;
        NearestBell = nearestBellObj;
        IsNearBell = nearestBellObj != null && nearestBellDistance <= plugin.Configuration.BellProximityDistance;

        // Auto-open window when approaching bell
        if (plugin.Configuration.AutoOpenNearBell && IsNearBell && !wasNearBell)
        {
            plugin.WindowSystem.Windows[0].IsOpen = true;
            if (plugin.Configuration.EnableDebugLogging)
            {
                Plugin.Log.Info($"Auto-opened window (distance: {DistanceToBell:F2})");
            }
        }

        wasNearBell = IsNearBell;
    }

    public void Dispose()
    {
        // Nothing to dispose
    }
}
