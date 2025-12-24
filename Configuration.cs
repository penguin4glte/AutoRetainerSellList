using Dalamud.Configuration;
using System;

namespace RetainerPriceAdjuster;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    // UI State
    public bool IsWindowOpen { get; set; } = false;
    public bool IsPriceAdjustmentEnabled { get; set; } = false;

    // Settings
    public bool AutoOpenNearBell { get; set; } = true;
    public float BellProximityDistance { get; set; } = 5.0f;
    public int DelayBetweenRetainers { get; set; } = 1000; // ms
    public int DelayBetweenPriceUpdates { get; set; } = 500; // ms

    // Debug
    public bool EnableDebugLogging { get; set; } = false;

    public void Save()
    {
        Plugin.PluginInterface?.SavePluginConfig(this);
    }
}
