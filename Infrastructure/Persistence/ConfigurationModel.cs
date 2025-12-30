using Dalamud.Configuration;

namespace AutoRetainerSellList.Infrastructure.Persistence;

[Serializable]
public class ConfigurationModel : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    // Auto-execute checkbox state
    public bool AutoExecuteEnabled { get; set; } = false;

    // Chat log language setting
    public string ChatLanguage { get; set; } = "Japanese";

    // Retainer-specific sell lists
    public Dictionary<ulong, RetainerSellListData> RetainerSellLists { get; set; } = new();
}

[Serializable]
public class RetainerSellListData
{
    public ulong RetainerId { get; set; }
    public string RetainerName { get; set; } = string.Empty;
    public List<SellListItemData> Items { get; set; } = new();

    public const int MaxItems = 20;
    public bool CanAddItem => Items.Count < MaxItems;
    public int RemainingSlots => MaxItems - Items.Count;
}

[Serializable]
public class SellListItemData
{
    public uint ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int QuantityToMaintain { get; set; } = 1;
    public string Guid { get; set; } = System.Guid.NewGuid().ToString();
}
