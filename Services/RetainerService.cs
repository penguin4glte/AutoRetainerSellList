using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ECommons.UIHelpers.AddonMasterImplementations;

namespace RetainerPriceAdjuster.Services;

public unsafe class RetainerService : IDisposable
{
    private readonly Plugin plugin;
    private bool selectStringCallbackSent = false;

    public int RetainerCount { get; private set; } = 0;
    public List<ListingInfo> CurrentListings { get; private set; } = new();

    public class ListingInfo
    {
        public int SlotIndex { get; set; }
        public uint ItemId { get; set; }
        public string ItemName { get; set; } = "";
        public bool IsHq { get; set; }
        public uint CurrentPrice { get; set; }
        public uint Quantity { get; set; }
    }

    private long lastTalkClickTime = 0;
    private const long TalkClickThrottle = 100; // ms between clicks
    private bool autoCloseTalkEnabled = true;

    public RetainerService(Plugin plugin)
    {
        this.plugin = plugin;

        // Register framework update to auto-close Talk dialogs
        Plugin.Framework.Update += AutoAdvanceTalkDialogue;
    }

    public void SetAutoCloseTalk(bool enabled)
    {
        autoCloseTalkEnabled = enabled;
    }

    public AtkUnitBase* GetAddon(string name)
    {
        var addonPtr = Plugin.GameGui.GetAddonByName(name);
        return (AtkUnitBase*)(nint)addonPtr;
    }

    private bool IsAddonReady(AtkUnitBase* addon)
    {
        return addon != null && addon->IsVisible;
    }

    public bool? OpenRetainerList()
    {
        // Check if we're at a retainer bell
        if (!plugin.BellProximityService.IsNearBell)
        {
            Plugin.Log.Warning("Not near a retainer bell");
            return false;
        }

        var retainerListAddon = GetAddon("RetainerList");
        if (IsAddonReady(retainerListAddon))
        {
            return true; // Already open
        }

        // Try to interact with bell
        var bell = plugin.BellProximityService.NearestBell;
        if (bell == null)
        {
            Plugin.Log.Warning("No bell object found");
            return false;
        }

        try
        {
            // Target the bell if not already targeted
            if (Plugin.TargetManager.Target?.EntityId != bell.EntityId)
            {
                Plugin.TargetManager.Target = bell;
                if (plugin.Configuration.EnableDebugLogging)
                {
                    Plugin.Log.Info($"Targeted retainer bell at {bell.Position}");
                }
                return null; // Wait for next frame
            }

            // Interact with the bell
            // Note: Direct interaction requires using TargetSystem from ClientStructs
            if (plugin.Configuration.EnableDebugLogging)
            {
                Plugin.Log.Info("Attempting to interact with retainer bell...");
            }

            // Use the FFXIVClientStructs TargetSystem to interact
            var targetSystem = FFXIVClientStructs.FFXIV.Client.Game.Control.TargetSystem.Instance();
            if (targetSystem != null)
            {
                var gameObject = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)bell.Address;
                targetSystem->InteractWithObject(gameObject);

                if (plugin.Configuration.EnableDebugLogging)
                {
                    Plugin.Log.Info("Sent interact command to bell");
                }

                return null; // Wait for retainer list to open
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error interacting with retainer bell");
            return false;
        }

        return null; // Keep waiting
    }

    public bool? GetRetainerCount()
    {
        var retainerListAddon = GetAddon("RetainerList");
        if (!IsAddonReady(retainerListAddon))
        {
            return false;
        }

        try
        {
            // The retainer list addon contains the retainer count
            // This is a simplified version - actual implementation needs proper parsing
            var atkArrayData = RaptureAtkModule.Instance()->AtkArrayDataHolder.NumberArrays[23];
            if (atkArrayData != null)
            {
                RetainerCount = atkArrayData->IntArray[0];
                if (plugin.Configuration.EnableDebugLogging)
                {
                    Plugin.Log.Info($"Retainer count: {RetainerCount}");
                }
                return true;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error getting retainer count");
        }

        return false;
    }

    public bool? SelectRetainer(int index)
    {
        var retainerListAddon = GetAddon("RetainerList");
        if (!IsAddonReady(retainerListAddon))
        {
            return false;
        }

        try
        {
            // Fire callback to select retainer
            var values = stackalloc AtkValue[2];
            values[0] = new AtkValue { Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int, Int = 2 };
            values[1] = new AtkValue { Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int, Int = index };

            retainerListAddon->FireCallback(2, values);

            if (plugin.Configuration.EnableDebugLogging)
            {
                Plugin.Log.Info($"Selected retainer {index}");
            }

            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, $"Error selecting retainer {index}");
            return false;
        }
    }

    public bool? CloseTalkAddon()
    {
        var talkAddon = GetAddon("Talk");
        if (IsAddonReady(talkAddon))
        {
            try
            {
                if (plugin.Configuration.EnableDebugLogging)
                {
                    Plugin.Log.Info("Talk addon still open, clicking to close");
                }

                // Try clicking the Talk addon to close it
                // This simulates clicking anywhere on the Talk window
                var values = stackalloc AtkValue[1];
                values[0] = new AtkValue { Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int, Int = -1 };

                talkAddon->FireCallback(1, values);

                if (plugin.Configuration.EnableDebugLogging)
                {
                    Plugin.Log.Info("Sent close command to Talk addon");
                }

                // Wait a frame to see if it closed
                return null;
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Error closing Talk addon");
                // Try force close as fallback
                try
                {
                    talkAddon->Close(true);
                    Plugin.Log.Info("Force closed Talk addon");
                }
                catch { }
                return false;
            }
        }

        // Talk is not open or already closed
        if (plugin.Configuration.EnableDebugLogging)
        {
            Plugin.Log.Info("Talk addon already closed");
        }
        return true;
    }

    public bool? WaitForRetainerMenu()
    {
        // Wait for Retainer menu to open
        // Talk dialogue is auto-advanced by AutoAdvanceTalkDialogue
        var retainerAddon = GetAddon("Retainer");
        if (IsAddonReady(retainerAddon))
        {
            if (plugin.Configuration.EnableDebugLogging)
            {
                Plugin.Log.Info("Retainer menu is now open");
            }
            selectStringCallbackSent = false; // Reset flag for next retainer
            return true;
        }

        // Still waiting for Retainer menu
        // Talk dialogue should be auto-advancing in the background
        return null; // Keep waiting
    }

    public bool? OpenSellList()
    {
        var sellListAddon = GetAddon("RetainerSellList");
        if (IsAddonReady(sellListAddon))
        {
            if (plugin.Configuration.EnableDebugLogging)
            {
                Plugin.Log.Info("RetainerSellList is already open");
            }
            selectStringCallbackSent = false; // Reset flag
            return true; // Already open
        }

        // Check if SelectString is open (retainer menu options)
        var selectStringAddon = GetAddon("SelectString");
        if (IsAddonReady(selectStringAddon))
        {
            // Only send callback once per SelectString window
            if (!selectStringCallbackSent)
            {
                try
                {
                    // Read the SelectString entries to find "View market listings"
                    var addonSelectString = (FFXIVClientStructs.FFXIV.Client.UI.AddonSelectString*)selectStringAddon;
                    var popupMenu = &addonSelectString->PopupMenu.PopupMenu;

                    if (plugin.Configuration.EnableDebugLogging)
                    {
                        Plugin.Log.Info($"SelectString menu is open with {popupMenu->EntryCount} entries:");
                        for (int i = 0; i < popupMenu->EntryCount; i++)
                        {
                            var entryPtr = (nint)popupMenu->EntryNames[i].Value;
                            if (entryPtr != nint.Zero)
                            {
                                var entryText = Marshal.PtrToStringUTF8(entryPtr);
                                Plugin.Log.Info($"  [{i}] {entryText}");
                            }
                        }
                    }

                    // Find the entry for "View market listings"
                    // Japanese: マーケットへの出品を確認
                    int targetIndex = -1;
                    for (int i = 0; i < popupMenu->EntryCount; i++)
                    {
                        var entryPtr = (nint)popupMenu->EntryNames[i].Value;
                        if (entryPtr != nint.Zero)
                        {
                            var entryText = Marshal.PtrToStringUTF8(entryPtr);
                            // Check for Japanese or English text containing market/出品
                            if (entryText != null && (entryText.Contains("出品") || entryText.Contains("market", StringComparison.OrdinalIgnoreCase)))
                            {
                                targetIndex = i;
                                Plugin.Log.Info($"Found market listings option at index {i}: {entryText}");
                                break;
                            }
                        }
                    }

                    if (targetIndex >= 0)
                    {
                        var values = stackalloc AtkValue[2];
                        values[0] = new AtkValue { Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int, Int = 0 };
                        values[1] = new AtkValue { Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int, Int = targetIndex };

                        selectStringAddon->FireCallback(2, values);

                        Plugin.Log.Info($"Selected market listings option at index {targetIndex}");
                        selectStringCallbackSent = true;
                    }
                    else
                    {
                        Plugin.Log.Error("Could not find market listings option in SelectString menu");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error(ex, "Error selecting from SelectString");
                    return false;
                }
            }

            return null; // Wait for sell list to open
        }

        // Reset flag when SelectString is not open
        selectStringCallbackSent = false;

        var retainerAddon = GetAddon("Retainer");
        if (!IsAddonReady(retainerAddon))
        {
            return null; // Still waiting for retainer menu
        }

        try
        {
            // Open the retainer menu options (should open SelectString)
            // Use callback to open menu
            var values = stackalloc AtkValue[2];
            values[0] = new AtkValue { Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int, Int = 0 };
            values[1] = new AtkValue { Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int, Int = 0 };

            retainerAddon->FireCallback(2, values);

            if (plugin.Configuration.EnableDebugLogging)
            {
                Plugin.Log.Info("Sent command to open retainer options menu");
            }

            return null; // Wait for SelectString to open
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error opening sell list");
            return false;
        }
    }

    public bool? GetCurrentListings()
    {
        var sellListAddon = GetAddon("RetainerSellList");
        if (!IsAddonReady(sellListAddon))
        {
            return false;
        }

        try
        {
            CurrentListings.Clear();

            // Parse the sell list addon to get current listings
            // The RetainerSellList addon shows up to 20 items
            // We need to check the AtkArrayData for item information

            var atkArrayData = RaptureAtkModule.Instance()->AtkArrayDataHolder.NumberArrays[15];
            if (atkArrayData != null)
            {
                // The array contains information about the listings
                // Check how many items are listed
                var itemCount = 0;
                for (int i = 0; i < 20; i++)
                {
                    // Each listing uses multiple array slots
                    // Check if there's an item ID at this position
                    var itemIdIndex = i * 10; // Each item uses approximately 10 slots
                    if (itemIdIndex + 1 >= atkArrayData->IntArray[0])
                        break;

                    var itemId = (uint)atkArrayData->IntArray[itemIdIndex + 1];
                    if (itemId == 0)
                        break;

                    var isHq = atkArrayData->IntArray[itemIdIndex + 2] != 0;
                    var currentPrice = (uint)atkArrayData->IntArray[itemIdIndex + 3];
                    var quantity = (uint)atkArrayData->IntArray[itemIdIndex + 4];

                    var itemSheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
                    var item = itemSheet?.GetRowOrDefault(itemId);
                    var itemName = item?.Name.ToString() ?? $"Item {itemId}";

                    CurrentListings.Add(new ListingInfo
                    {
                        SlotIndex = i,
                        ItemId = itemId,
                        ItemName = itemName,
                        IsHq = isHq,
                        CurrentPrice = currentPrice,
                        Quantity = quantity
                    });

                    itemCount++;
                }

                if (plugin.Configuration.EnableDebugLogging)
                {
                    Plugin.Log.Info($"Found {itemCount} listings");
                    foreach (var listing in CurrentListings)
                    {
                        Plugin.Log.Info($"  Slot {listing.SlotIndex}: {listing.ItemName} ({(listing.IsHq ? "HQ" : "NQ")}) " +
                                      $"x{listing.Quantity} @ {listing.CurrentPrice} gil");
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error getting current listings");
            return false;
        }
    }

    public bool? ClickListingItem(int slotIndex)
    {
        var sellListAddon = GetAddon("RetainerSellList");
        if (!IsAddonReady(sellListAddon))
        {
            return false;
        }

        try
        {
            // Click on the listing to open it (this will trigger MarketBuddy if installed)
            var values = stackalloc AtkValue[2];
            values[0] = new AtkValue { Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int, Int = 0 };
            values[1] = new AtkValue { Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int, Int = slotIndex };

            sellListAddon->FireCallback(2, values);

            if (plugin.Configuration.EnableDebugLogging)
            {
                Plugin.Log.Info($"Clicked listing at slot {slotIndex}");
            }

            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, $"Error clicking listing at slot {slotIndex}");
            return false;
        }
    }

    public bool? WaitForItemSearchResult()
    {
        // Check if ItemSearchResult is open (MarketBuddy opens this)
        var itemSearchResult = GetAddon("ItemSearchResult");
        if (IsAddonReady(itemSearchResult))
        {
            if (plugin.Configuration.EnableDebugLogging)
            {
                Plugin.Log.Info("ItemSearchResult window is open");
            }
            return true;
        }

        // If MarketBuddy is not installed, InputNumeric might open directly
        var inputNumeric = GetAddon("InputNumeric");
        if (IsAddonReady(inputNumeric))
        {
            if (plugin.Configuration.EnableDebugLogging)
            {
                Plugin.Log.Info("InputNumeric window is open (MarketBuddy not detected)");
            }
            return true;
        }

        return null; // Still waiting
    }

    public bool? CloseItemSearchResult()
    {
        var itemSearchResult = GetAddon("ItemSearchResult");
        if (IsAddonReady(itemSearchResult))
        {
            try
            {
                itemSearchResult->Close(true);
                if (plugin.Configuration.EnableDebugLogging)
                {
                    Plugin.Log.Info("Closed ItemSearchResult window");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Error closing ItemSearchResult");
            }
        }

        return true;
    }

    public bool? WaitForInputNumeric()
    {
        var inputNumeric = GetAddon("InputNumeric");
        if (IsAddonReady(inputNumeric))
        {
            if (plugin.Configuration.EnableDebugLogging)
            {
                Plugin.Log.Info("InputNumeric dialog is ready");
            }
            return true;
        }

        return null; // Still waiting
    }

    public bool? SetPriceInDialog(uint newPrice)
    {
        var inputNumeric = GetAddon("InputNumeric");
        if (!IsAddonReady(inputNumeric))
        {
            return false;
        }

        try
        {
            // Set the new price in the InputNumeric dialog
            var values = stackalloc AtkValue[4];
            values[0] = new AtkValue { Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int, Int = 3 };
            values[1] = new AtkValue { Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.UInt, UInt = newPrice };
            values[2] = new AtkValue { Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.UInt, UInt = 0 };
            values[3] = new AtkValue { Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.UInt, UInt = 0 };

            inputNumeric->FireCallback(4, values);

            Plugin.Log.Info($"Set price to {newPrice} gil");
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error setting price in dialog");
            return false;
        }
    }

    public bool? ConfirmPriceDialog()
    {
        var inputNumeric = GetAddon("InputNumeric");
        if (IsAddonReady(inputNumeric))
        {
            try
            {
                // Confirm the price change (click OK)
                var values = stackalloc AtkValue[4];
                values[0] = new AtkValue { Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int, Int = 0 };
                values[1] = new AtkValue { Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.UInt, UInt = 0 };
                values[2] = new AtkValue { Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.UInt, UInt = 0 };
                values[3] = new AtkValue { Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.UInt, UInt = 0 };

                inputNumeric->FireCallback(4, values);

                Plugin.Log.Info("Confirmed price change");
                return null; // Wait for dialog to close
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Error confirming price dialog");
                return false;
            }
        }

        // Dialog is closed, price update complete
        return true;
    }

    public bool? CancelInputNumericDialog()
    {
        var inputNumeric = GetAddon("InputNumeric");
        if (IsAddonReady(inputNumeric))
        {
            try
            {
                inputNumeric->Close(true);
                Plugin.Log.Info("Cancelled InputNumeric dialog");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Error cancelling InputNumeric dialog");
            }
        }
        return true;
    }

    public bool? CloseRetainer()
    {
        // Close RetainerSellList if it's open
        var sellListAddon = GetAddon("RetainerSellList");
        if (IsAddonReady(sellListAddon))
        {
            try
            {
                // Click the X button to close (callback -1)
                var values = stackalloc AtkValue[1];
                values[0] = new AtkValue { Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int, Int = -1 };
                sellListAddon->FireCallback(1, values);

                if (plugin.Configuration.EnableDebugLogging)
                {
                    Plugin.Log.Info("Sent close command to RetainerSellList");
                }

                return null; // Wait for it to close
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Error closing RetainerSellList");
                // Force close as fallback
                sellListAddon->Close(true);
            }
        }

        // Close Retainer menu if it's open
        var retainerAddon = GetAddon("Retainer");
        if (IsAddonReady(retainerAddon))
        {
            try
            {
                retainerAddon->Close(true);
                if (plugin.Configuration.EnableDebugLogging)
                {
                    Plugin.Log.Info("Closed Retainer menu");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Error closing Retainer menu");
            }
        }

        return true; // Closed or already closed
    }

    public bool? CloseRetainerList()
    {
        var retainerListAddon = GetAddon("RetainerList");
        if (IsAddonReady(retainerListAddon))
        {
            retainerListAddon->Close(true);
            return true;
        }

        return true; // Already closed
    }

    private void AutoAdvanceTalkDialogue(Dalamud.Plugin.Services.IFramework framework)
    {
        // Only auto-advance Talk when task manager is running
        if (!plugin.TaskManager.IsRunning || !autoCloseTalkEnabled)
            return;

        var talkAddon = GetAddon("Talk");
        if (!IsAddonReady(talkAddon))
            return;

        // Throttle clicks to prevent spamming
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (now - lastTalkClickTime < TalkClickThrottle)
            return;

        try
        {
            // Use ECommons AddonMaster to click on Talk dialogue
            // This is the same method AutoRetainer uses
            if (ECommons.GenericHelpers.TryGetAddonByName<AddonTalk>("Talk", out var addon) &&
                ECommons.GenericHelpers.IsAddonReady(&addon->AtkUnitBase))
            {
                new AddonMaster.Talk((nint)addon).Click();

                lastTalkClickTime = now;

                if (plugin.Configuration.EnableDebugLogging)
                {
                    Plugin.Log.Info("Clicked Talk dialogue using ECommons");
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error clicking Talk dialogue");
        }
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= AutoAdvanceTalkDialogue;
        CurrentListings.Clear();
    }
}
