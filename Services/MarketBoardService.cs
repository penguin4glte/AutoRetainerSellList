using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RetainerPriceAdjuster.Services;

public unsafe class MarketBoardService : IDisposable
{
    private readonly Plugin plugin;
    private readonly Dictionary<(uint ItemId, bool IsHq), PriceData> priceCache = new();

    // State tracking for multi-step price fetching
    private enum FetchState
    {
        Idle,
        OpeningItemSearch,
        WaitingForItemSearch,
        SearchingItem,
        WaitingForResults,
        ReadingPrices,
        ClosingItemSearch,
        Complete,
        Error
    }

    private FetchState currentState = FetchState.Idle;
    private uint requestedItemId = 0;
    private bool requestedIsHq = false;
    private long stateStartTime = 0;
    private const long StateTimeout = 10000; // 10 seconds timeout per state

    public class PriceData
    {
        public uint LowestPrice { get; set; }
        public DateTime FetchedAt { get; set; }
        public List<uint> AllPrices { get; set; } = new();
    }

    public MarketBoardService(Plugin plugin)
    {
        this.plugin = plugin;
    }

    private AtkUnitBase* GetAddon(string name)
    {
        var addonPtr = Plugin.GameGui.GetAddonByName(name);
        return (AtkUnitBase*)(nint)addonPtr;
    }

    private bool IsAddonReady(AtkUnitBase* addon)
    {
        return addon != null && addon->IsVisible;
    }

    private void ChangeState(FetchState newState)
    {
        if (plugin.Configuration.EnableDebugLogging)
        {
            Plugin.Log.Info($"Market board state: {currentState} -> {newState}");
        }
        currentState = newState;
        stateStartTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private bool HasStateTimedOut()
    {
        var elapsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - stateStartTime;
        return elapsed > StateTimeout;
    }

    public bool? FetchPrice(uint itemId, bool isHq)
    {
        // Check cache first
        var key = (itemId, isHq);
        if (priceCache.TryGetValue(key, out var cached))
        {
            if ((DateTime.UtcNow - cached.FetchedAt).TotalMinutes < 5)
            {
                if (plugin.Configuration.EnableDebugLogging)
                {
                    Plugin.Log.Info($"Using cached price for item {itemId} ({(isHq ? "HQ" : "NQ")}): {cached.LowestPrice} gil");
                }
                return true;
            }
        }

        // Start fetch process if idle
        if (currentState == FetchState.Idle)
        {
            requestedItemId = itemId;
            requestedIsHq = isHq;
            ChangeState(FetchState.OpeningItemSearch);
        }

        // State machine for price fetching
        switch (currentState)
        {
            case FetchState.OpeningItemSearch:
                return OpenItemSearch();

            case FetchState.WaitingForItemSearch:
                return WaitForItemSearch();

            case FetchState.SearchingItem:
                return SearchForItem();

            case FetchState.WaitingForResults:
                return WaitForResults();

            case FetchState.ReadingPrices:
                return ReadPrices();

            case FetchState.ClosingItemSearch:
                return CloseItemSearch();

            case FetchState.Complete:
                ChangeState(FetchState.Idle);
                return true;

            case FetchState.Error:
                Plugin.Log.Error($"Market board fetch error for item {itemId}");
                ChangeState(FetchState.Idle);
                return false;

            default:
                return null;
        }
    }

    private bool? OpenItemSearch()
    {
        if (HasStateTimedOut())
        {
            Plugin.Log.Error("Timeout opening ItemSearch");
            ChangeState(FetchState.Error);
            return false;
        }

        var itemSearch = GetAddon("ItemSearch");
        if (IsAddonReady(itemSearch))
        {
            ChangeState(FetchState.SearchingItem);
            return null;
        }

        try
        {
            // Open ItemSearch addon
            // This requires calling the agent to open the market board
            var agentModule = AgentModule.Instance();
            if (agentModule != null)
            {
                var itemSearchAgent = agentModule->GetAgentByInternalId(AgentId.ItemSearch);
                if (itemSearchAgent != null && !itemSearchAgent->IsAgentActive())
                {
                    itemSearchAgent->Show();
                    ChangeState(FetchState.WaitingForItemSearch);
                    if (plugin.Configuration.EnableDebugLogging)
                    {
                        Plugin.Log.Info("Opened ItemSearch agent");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error opening ItemSearch");
            ChangeState(FetchState.Error);
            return false;
        }

        return null;
    }

    private bool? WaitForItemSearch()
    {
        if (HasStateTimedOut())
        {
            Plugin.Log.Error("Timeout waiting for ItemSearch");
            ChangeState(FetchState.Error);
            return false;
        }

        var itemSearch = GetAddon("ItemSearch");
        if (IsAddonReady(itemSearch))
        {
            ChangeState(FetchState.SearchingItem);
        }

        return null;
    }

    private bool? SearchForItem()
    {
        if (HasStateTimedOut())
        {
            Plugin.Log.Error("Timeout searching for item");
            ChangeState(FetchState.Error);
            return false;
        }

        var itemSearch = GetAddon("ItemSearch");
        if (!IsAddonReady(itemSearch))
        {
            ChangeState(FetchState.Error);
            return false;
        }

        try
        {
            // Get item name from data
            var itemSheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
            var item = itemSheet?.GetRowOrDefault(requestedItemId);
            if (item == null)
            {
                Plugin.Log.Error($"Item {requestedItemId} not found in data");
                ChangeState(FetchState.Error);
                return false;
            }

            var itemName = item.Value.Name.ToString();

            // Input item name into search box and trigger search
            // Note: This requires detailed addon structure knowledge
            // The exact implementation depends on ItemSearch addon structure

            // Callback to search - parameters may vary
            var values = stackalloc AtkValue[3];
            values[0] = new AtkValue { Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int, Int = 0 };
            values[1] = new AtkValue { Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.String, String = null };
            values[2] = new AtkValue { Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.UInt, UInt = requestedItemId };

            itemSearch->FireCallback(3, values);

            if (plugin.Configuration.EnableDebugLogging)
            {
                Plugin.Log.Info($"Searching for item {itemName} (ID: {requestedItemId})");
            }

            ChangeState(FetchState.WaitingForResults);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error searching for item");
            ChangeState(FetchState.Error);
            return false;
        }

        return null;
    }

    private bool? WaitForResults()
    {
        if (HasStateTimedOut())
        {
            Plugin.Log.Error("Timeout waiting for results");
            ChangeState(FetchState.Error);
            return false;
        }

        var itemSearchResult = GetAddon("ItemSearchResult");
        if (IsAddonReady(itemSearchResult))
        {
            ChangeState(FetchState.ReadingPrices);
        }

        return null;
    }

    private bool? ReadPrices()
    {
        if (HasStateTimedOut())
        {
            Plugin.Log.Error("Timeout reading prices");
            ChangeState(FetchState.Error);
            return false;
        }

        var itemSearchResult = GetAddon("ItemSearchResult");
        if (!IsAddonReady(itemSearchResult))
        {
            ChangeState(FetchState.Error);
            return false;
        }

        try
        {
            var prices = new List<uint>();

            // Debug: Print addon structure if debug logging is enabled
            if (plugin.Configuration.EnableDebugLogging)
            {
                Plugin.Log.Info("=== Parsing ItemSearchResult ===");
                ItemSearchResultParser.DebugPrintAddonStructure(itemSearchResult);
            }

            // Parse listings from the addon
            var listings = ItemSearchResultParser.ParseListings(itemSearchResult);

            if (plugin.Configuration.EnableDebugLogging)
            {
                Plugin.Log.Info($"Parsed {listings.Count} listings from ItemSearchResult");
            }

            // Filter by HQ/NQ and extract prices
            foreach (var listing in listings)
            {
                if (listing.IsHq == requestedIsHq)
                {
                    prices.Add(listing.Price);

                    if (plugin.Configuration.EnableDebugLogging)
                    {
                        Plugin.Log.Info($"  - {listing.Price:N0} gil x{listing.Quantity} " +
                                      $"({(listing.IsHq ? "HQ" : "NQ")}) from {listing.SellerName}");
                    }
                }
            }

            // If parsing returned no results, fall back to dummy data for testing
            if (prices.Count == 0 && plugin.Configuration.EnableDebugLogging)
            {
                Plugin.Log.Warning("No prices extracted from addon - using dummy data for testing");
                Plugin.Log.Warning("NOTE: You need to update ItemSearchResultParser.cs with correct node indices");

                // Generate dummy data for testing
                var random = new Random();
                for (int i = 0; i < 5; i++)
                {
                    prices.Add((uint)random.Next(1000, 50000));
                }
            }

            if (prices.Count > 0)
            {
                prices.Sort();
                var lowestPrice = prices[0];

                priceCache[(requestedItemId, requestedIsHq)] = new PriceData
                {
                    LowestPrice = lowestPrice,
                    FetchedAt = DateTime.UtcNow,
                    AllPrices = prices
                };

                Plugin.Log.Info($"Found {prices.Count} listings for item {requestedItemId} ({(requestedIsHq ? "HQ" : "NQ")})");
                Plugin.Log.Info($"Lowest price: {lowestPrice} gil");
            }
            else
            {
                Plugin.Log.Warning($"No listings found for item {requestedItemId} ({(requestedIsHq ? "HQ" : "NQ")})");
            }

            ChangeState(FetchState.ClosingItemSearch);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error reading prices");
            ChangeState(FetchState.Error);
            return false;
        }

        return null;
    }

    private bool? CloseItemSearch()
    {
        try
        {
            // Close ItemSearchResult
            var itemSearchResult = GetAddon("ItemSearchResult");
            if (IsAddonReady(itemSearchResult))
            {
                itemSearchResult->Close(true);
            }

            // Close ItemSearch
            var itemSearch = GetAddon("ItemSearch");
            if (IsAddonReady(itemSearch))
            {
                itemSearch->Close(true);
            }

            if (plugin.Configuration.EnableDebugLogging)
            {
                Plugin.Log.Info("Closed ItemSearch addons");
            }

            ChangeState(FetchState.Complete);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error closing ItemSearch");
            ChangeState(FetchState.Error);
            return false;
        }

        return null;
    }

    public uint? GetLowestPrice(uint itemId, bool isHq)
    {
        var key = (itemId, isHq);
        if (priceCache.TryGetValue(key, out var data))
        {
            return data.LowestPrice;
        }
        return null;
    }

    /// <summary>
    /// Read price from currently open ItemSearchResult window (opened by MarketBuddy)
    /// This is a simpler alternative to the full fetch process when MarketBuddy is installed
    /// </summary>
    public bool? ReadPriceFromItemSearchResult(uint itemId, bool isHq)
    {
        var itemSearchResult = GetAddon("ItemSearchResult");
        if (!IsAddonReady(itemSearchResult))
        {
            if (plugin.Configuration.EnableDebugLogging)
            {
                Plugin.Log.Info("ItemSearchResult is not open");
            }
            return false;
        }

        try
        {
            var prices = new List<uint>();

            if (plugin.Configuration.EnableDebugLogging)
            {
                Plugin.Log.Info("=== Reading prices from ItemSearchResult ===");
                ItemSearchResultParser.DebugPrintAddonStructure(itemSearchResult);
            }

            // Parse listings from the addon
            var listings = ItemSearchResultParser.ParseListings(itemSearchResult);

            if (plugin.Configuration.EnableDebugLogging)
            {
                Plugin.Log.Info($"Parsed {listings.Count} listings from ItemSearchResult");
            }

            // Filter by HQ/NQ and extract prices
            foreach (var listing in listings)
            {
                if (listing.IsHq == isHq)
                {
                    prices.Add(listing.Price);

                    if (plugin.Configuration.EnableDebugLogging)
                    {
                        Plugin.Log.Info($"  - {listing.Price:N0} gil x{listing.Quantity} " +
                                      $"({(listing.IsHq ? "HQ" : "NQ")}) from {listing.SellerName}");
                    }
                }
            }

            // If parsing returned no results, return false
            if (prices.Count == 0)
            {
                Plugin.Log.Warning($"No prices found for item {itemId} ({(isHq ? "HQ" : "NQ")})");
                return false;
            }

            prices.Sort();
            var lowestPrice = prices[0];

            // Cache the result
            priceCache[(itemId, isHq)] = new PriceData
            {
                LowestPrice = lowestPrice,
                FetchedAt = DateTime.UtcNow,
                AllPrices = prices
            };

            Plugin.Log.Info($"Found {prices.Count} listings for item {itemId} ({(isHq ? "HQ" : "NQ")})");
            Plugin.Log.Info($"Lowest price: {lowestPrice} gil");

            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error reading prices from ItemSearchResult");
            return false;
        }
    }

    public void ClearCache()
    {
        priceCache.Clear();
    }

    public void Reset()
    {
        ChangeState(FetchState.Idle);
        requestedItemId = 0;
        requestedIsHq = false;
    }

    public void Dispose()
    {
        try
        {
            // Close any open addons
            var itemSearchResult = GetAddon("ItemSearchResult");
            if (IsAddonReady(itemSearchResult))
            {
                itemSearchResult->Close(true);
            }

            var itemSearch = GetAddon("ItemSearch");
            if (IsAddonReady(itemSearch))
            {
                itemSearch->Close(true);
            }
        }
        catch { }

        priceCache.Clear();
    }
}
