using AutoRetainerSellList.Domain.ValueObjects;
using AutoRetainerSellList.Infrastructure.Localization;
using Dalamud.Game.Text.SeStringHandling;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using static ECommons.GenericHelpers;
using ECommons.Automation;

namespace AutoRetainerSellList.Infrastructure.GameClient;

public unsafe class GameUIService
{
    private readonly MarketBoardService _marketBoardService;
    private readonly ChatMessageService _chatMessageService;

    public GameUIService(MarketBoardService marketBoardService, ChatMessageService chatMessageService)
    {
        _marketBoardService = marketBoardService;
        _chatMessageService = chatMessageService;
    }

    public bool OpenItemContextMenu(InventoryType inventoryType, uint slot)
    {
        try
        {

            var agentModule = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentModule.Instance();
            var agent = (FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentInventoryContext*)agentModule->GetAgentByInternalId(
                FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentId.InventoryContext);

            if (agent == null)
            {
                Svc.Log.Error("[GameUIService] AgentInventoryContext is null");
                return false;
            }

            agent->OpenForItemSlot(inventoryType, (int)slot, 0, 0);
            return true;
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[GameUIService] Error opening context menu: {ex}");
            return false;
        }
    }

    public bool ClickPutUpForSale()
    {
        try
        {
            if (TryGetAddonMaster<ECommons.UIHelpers.AddonMasterImplementations.AddonMaster.ContextMenu>(out var contextMenu) && contextMenu.IsAddonReady)
            {

                var putUpForSaleText = "Put Up for Sale";
                try
                {
                    var addonSheet = Svc.Data.GetExcelSheet<Addon>();
                    if (addonSheet != null)
                    {
                        var addonRow = addonSheet.GetRow(99);
                        putUpForSaleText = addonRow.Text.ToString();
                    }
                }
                catch
                {
                    // Use default English text if sheet reading fails
                }


                var entries = contextMenu.Entries.ToList();

                foreach (var entry in entries)
                {

                    if (entry.Text.Contains(putUpForSaleText, StringComparison.OrdinalIgnoreCase) && entry.Enabled)
                    {
                        entry.Select();
                        return true;
                    }
                }

                Svc.Log.Error("[GameUIService] 'Put Up for Sale' option not found in context menu");
                return true; // Skip this item
            }

            return false;
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[GameUIService] Error clicking Put Up for Sale: {ex}");
            return false;
        }
    }

    public bool WaitForRetainerSell()
    {
        if (TryGetAddonByName<AddonRetainerSell>("RetainerSell", out var addon) && IsAddonReady(&addon->AtkUnitBase))
        {
            return true;
        }
        return false;
    }

    public bool RequestMarketBoardPrice(ItemId itemId)
    {
        try
        {
            if (TryGetAddonByName<AddonRetainerSell>("RetainerSell", out var addon) && IsAddonReady(&addon->AtkUnitBase))
            {
                // Notify MarketBoardService that we're starting a request
                _marketBoardService.BeginRequest(itemId);

                // Click "Compare Prices" button (callback 4)
                ECommons.Automation.Callback.Fire(&addon->AtkUnitBase, true, 4);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[GameUIService] Error requesting market board price: {ex}");
            return false;
        }
    }

    public bool SetPriceAndConfirm(Price price, ItemId itemId, string itemName)
    {
        try
        {
            // Close ItemSearchResult if open
            if (TryGetAddonByName<AtkUnitBase>("ItemSearchResult", out var searchAddon))
            {
                searchAddon->Close(true);
            }

            if (TryGetAddonByName<AddonRetainerSell>("RetainerSell", out var addon) && IsAddonReady(&addon->AtkUnitBase))
            {
                if (price.Value > 0)
                {
                    addon->AskingPrice->SetValue(price.Value);

                    // Confirm (callback 0)
                    ECommons.Automation.Callback.Fire(&addon->AtkUnitBase, true, 0);
                    addon->AtkUnitBase.Close(true);

                    var messageText = _chatMessageService.GetItemListedMessageAsync(price.Value).GetAwaiter().GetResult();
                    var chatMessage = new SeStringBuilder()
                        .AddItemLink(itemId, false)
                        .AddText(messageText)
                        .Build();
                    Svc.Chat.Print(chatMessage);
                }
                else
                {
                    Svc.Log.Warning($"[GameUIService] No valid price for {itemName}, canceling");
                    // Cancel (callback 1)
                    ECommons.Automation.Callback.Fire(&addon->AtkUnitBase, true, 1);
                    addon->AtkUnitBase.Close(true);
                }

                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[GameUIService] Error setting price: {ex}");
            return false;
        }
    }

    public bool CancelRetainerSell()
    {
        try
        {
            // Close ItemSearchResult if open
            if (TryGetAddonByName<AtkUnitBase>("ItemSearchResult", out var searchAddon))
            {
                searchAddon->Close(true);
            }

            if (TryGetAddonByName<AddonRetainerSell>("RetainerSell", out var addon) && IsAddonReady(&addon->AtkUnitBase))
            {
                // Cancel (callback 1)
                ECommons.Automation.Callback.Fire(&addon->AtkUnitBase, true, 1);
                addon->AtkUnitBase.Close(true);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[GameUIService] Error canceling RetainerSell: {ex}");
            return false;
        }
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
            Svc.Log.Error($"[GameUIService] Error closing addon {addonName}: {ex}");
            return false;
        }
    }

    public bool IsAddonOpen(string addonName)
    {
        return TryGetAddonByName<AtkUnitBase>(addonName, out var addon) && IsAddonReady(addon);
    }

    public void PrintChatMessage(string message)
    {
        Svc.Chat.Print(message);
    }

    public void PrintItemMessage(uint itemId, string message)
    {
        var chatMessage = new SeStringBuilder()
            .AddItemLink(itemId, false)
            .AddText(message)
            .Build();
        Svc.Chat.Print(chatMessage);
    }

    public void PrintErrorMessage(uint itemId, string message)
    {
        var chatMessage = new SeStringBuilder()
            .AddItemLink(itemId, false)
            .AddText(message)
            .Build();
        Svc.Chat.PrintError(chatMessage);
    }

    public bool SelectRetainerFromList(int retainerIndex)
    {
        try
        {
            if (TryGetAddonByName<AtkUnitBase>("RetainerList", out var addon) && IsAddonReady(addon))
            {
                // Click on retainer entry (callback 2, with retainer index)
                ECommons.Automation.Callback.Fire(addon, true, 2, retainerIndex);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[GameUIService] Error selecting retainer: {ex}");
            return false;
        }
    }

    public bool OpenCheckSellingItems()
    {
        try
        {
            if (TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) && IsAddonReady(addon))
            {

                if (TryGetAddonMaster<ECommons.UIHelpers.AddonMasterImplementations.AddonMaster.SelectString>(out var selectString))
                {
                    var entries = selectString.Entries.ToList();

                    // Log all entries first
                    for (int i = 0; i < entries.Count; i++)
                    {
                        var entry = entries[i];
                    }

                    // Now find the right one
                    for (int i = 0; i < entries.Count; i++)
                    {
                        var entry = entries[i];

                        // Look for "マーケット出品（リテイナー所持品から）" (Sell items on market from retainer inventory)
                        // Must contain both "出品" and "リテイナー" for Japanese, or "Sell" and "retainer" for English
                        bool isJapaneseMatch = entry.Text.Contains("出品") && entry.Text.Contains("リテイナー");
                        bool isEnglishMatch = entry.Text.Contains("Sell", StringComparison.OrdinalIgnoreCase) &&
                                            entry.Text.Contains("retainer", StringComparison.OrdinalIgnoreCase);


                        if (isJapaneseMatch || isEnglishMatch)
                        {
                            // Use AddonMaster.SelectString API (same as Dagobert)
                            entry.Select();
                            return true;
                        }
                    }

                    Svc.Log.Error("[GameUIService] Could not find 'Sell items from retainer' option in SelectString menu");
                }
                return false;
            }

            return false;
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[GameUIService] Error opening check selling items: {ex}");
            return false;
        }
    }

    public bool WaitForRetainerSellListAddon()
    {
        if (TryGetAddonByName<AtkUnitBase>("RetainerSellList", out var addon) && IsAddonReady(addon))
        {
            return true;
        }
        return false;
    }

    public bool CloseRetainerSellListAddon()
    {
        return CloseAddon("RetainerSellList");
    }

    public bool CloseRetainerMenu()
    {
        try
        {
            if (TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) && IsAddonReady(addon))
            {
                addon->Close(true);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[GameUIService] Error closing retainer menu: {ex}");
            return false;
        }
    }
}
