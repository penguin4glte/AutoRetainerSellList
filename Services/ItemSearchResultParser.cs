using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;

namespace RetainerPriceAdjuster.Services;

/// <summary>
/// Helper class to parse ItemSearchResult addon structure
/// This requires reverse engineering the addon to determine exact node indices
/// </summary>
public unsafe class ItemSearchResultParser
{
    public class ListingEntry
    {
        public uint Price { get; set; }
        public uint Quantity { get; set; }
        public bool IsHq { get; set; }
        public string SellerName { get; set; } = "";
        public uint Total { get; set; }
    }

    /// <summary>
    /// Parses all listings from ItemSearchResult addon
    /// NOTE: This is a TEMPLATE implementation that needs to be filled in with actual node indices
    /// </summary>
    public static List<ListingEntry> ParseListings(AtkUnitBase* addon)
    {
        var listings = new List<ListingEntry>();

        if (addon == null || !addon->IsVisible)
        {
            return listings;
        }

        try
        {
            // IMPORTANT: The following indices are PLACEHOLDERS
            // You need to determine the correct values by:
            // 1. Opening ItemSearchResult in game
            // 2. Using Dalamud Inspector or SimpleTweaks AddonInspector
            // 3. Finding the list component that contains the price listings
            // 4. Identifying the node indices for price, HQ flag, quantity, etc.

            // Example structure (VERIFY THIS):
            // - ItemSearchResult has a component list (AtkComponentList)
            // - The list contains entries for each market board listing
            // - Each entry has child nodes for: seller name, price, quantity, HQ icon, etc.

            // Step 1: Find the list component
            // The list component index needs to be determined
            const int LIST_COMPONENT_INDEX = 999; // PLACEHOLDER - VERIFY THIS

            if (addon->UldManager.NodeListCount <= LIST_COMPONENT_INDEX)
            {
                Plugin.Log.Warning($"ItemSearchResult: Node list too short ({addon->UldManager.NodeListCount})");
                return listings;
            }

            var listNode = addon->UldManager.NodeList[LIST_COMPONENT_INDEX];
            if (listNode == null)
            {
                Plugin.Log.Warning("ItemSearchResult: List node is null");
                return listings;
            }

            // Check if this is actually a component list
            if (listNode->Type < NodeType.Res)
            {
                Plugin.Log.Warning("ItemSearchResult: List node is not a component");
                return listings;
            }

            var componentNode = (AtkComponentNode*)listNode;
            if (componentNode == null)
            {
                Plugin.Log.Warning("ItemSearchResult: Failed to get component node");
                return listings;
            }

            var component = componentNode->Component;
            if (component == null)
            {
                Plugin.Log.Warning("ItemSearchResult: Component is null");
                return listings;
            }

            // Try to cast to AtkComponentList (this is the typical structure for lists)
            var componentList = (AtkComponentList*)component;

            // Step 2: Iterate through list entries
            // The list shows multiple market board listings
            for (int i = 0; i < componentList->ListLength && i < 20; i++) // Max 20 listings typically
            {
                try
                {
                    var entry = ParseListingEntry(componentList, i);
                    if (entry != null)
                    {
                        listings.Add(entry);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error(ex, $"Error parsing listing entry {i}");
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error parsing ItemSearchResult listings");
        }

        return listings;
    }

    private static ListingEntry? ParseListingEntry(AtkComponentList* list, int index)
    {
        // IMPORTANT: These node indices are PLACEHOLDERS
        // You need to determine the correct values for your game version

        // Each list entry typically contains child nodes:
        // - Text node for seller name
        // - Text node for price
        // - Text node for quantity
        // - Image node for HQ icon (visible if HQ)
        // - etc.

        try
        {
            // Get the list item renderer component
            // This structure needs to be verified
            var renderer = list->ItemRendererList[index];
            if (renderer.AtkComponentListItemRenderer == null)
            {
                return null;
            }

            var itemRenderer = renderer.AtkComponentListItemRenderer;
            var uld = &itemRenderer->AtkComponentButton.AtkComponentBase.UldManager;

            if (uld->NodeListCount == 0)
            {
                return null;
            }

            // PLACEHOLDERS - VERIFY THESE INDICES
            const int PRICE_NODE_INDEX = 999;      // Text node containing price
            const int QUANTITY_NODE_INDEX = 999;   // Text node containing quantity
            const int HQ_ICON_NODE_INDEX = 999;    // Image node for HQ icon (visible = HQ)
            const int SELLER_NODE_INDEX = 999;     // Text node containing seller name

            var entry = new ListingEntry();

            // Parse price
            if (uld->NodeListCount > PRICE_NODE_INDEX)
            {
                var priceNode = uld->NodeList[PRICE_NODE_INDEX];
                if (priceNode != null && priceNode->Type == NodeType.Text)
                {
                    var textNode = priceNode->GetAsAtkTextNode();
                    if (textNode != null)
                    {
                        var priceText = textNode->NodeText.ToString();
                        // Remove commas and parse
                        priceText = priceText.Replace(",", "");
                        if (uint.TryParse(priceText, out var price))
                        {
                            entry.Price = price;
                        }
                    }
                }
            }

            // Parse quantity
            if (uld->NodeListCount > QUANTITY_NODE_INDEX)
            {
                var qtyNode = uld->NodeList[QUANTITY_NODE_INDEX];
                if (qtyNode != null && qtyNode->Type == NodeType.Text)
                {
                    var textNode = qtyNode->GetAsAtkTextNode();
                    if (textNode != null)
                    {
                        var qtyText = textNode->NodeText.ToString();
                        if (uint.TryParse(qtyText, out var qty))
                        {
                            entry.Quantity = qty;
                        }
                    }
                }
            }

            // Parse HQ flag (check if HQ icon is visible)
            if (uld->NodeListCount > HQ_ICON_NODE_INDEX)
            {
                var hqNode = uld->NodeList[HQ_ICON_NODE_INDEX];
                if (hqNode != null)
                {
                    entry.IsHq = hqNode->IsVisible();
                }
            }

            // Parse seller name
            if (uld->NodeListCount > SELLER_NODE_INDEX)
            {
                var sellerNode = uld->NodeList[SELLER_NODE_INDEX];
                if (sellerNode != null && sellerNode->Type == NodeType.Text)
                {
                    var textNode = sellerNode->GetAsAtkTextNode();
                    if (textNode != null)
                    {
                        entry.SellerName = textNode->NodeText.ToString();
                    }
                }
            }

            entry.Total = entry.Price * entry.Quantity;

            return entry;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, $"Error parsing list entry {index}");
            return null;
        }
    }

    /// <summary>
    /// Gets the lowest price from listings, optionally filtering by HQ
    /// </summary>
    public static uint? GetLowestPrice(List<ListingEntry> listings, bool? filterHq = null)
    {
        if (listings.Count == 0)
        {
            return null;
        }

        var filtered = listings;
        if (filterHq.HasValue)
        {
            filtered = new List<ListingEntry>();
            foreach (var listing in listings)
            {
                if (listing.IsHq == filterHq.Value)
                {
                    filtered.Add(listing);
                }
            }
        }

        if (filtered.Count == 0)
        {
            return null;
        }

        uint lowest = uint.MaxValue;
        foreach (var listing in filtered)
        {
            if (listing.Price < lowest)
            {
                lowest = listing.Price;
            }
        }

        return lowest == uint.MaxValue ? null : lowest;
    }

    /// <summary>
    /// Helper to print addon structure for debugging
    /// Use this in-game with Dalamud Inspector to find correct node indices
    /// </summary>
    public static void DebugPrintAddonStructure(AtkUnitBase* addon, int maxDepth = 3)
    {
        if (addon == null)
        {
            Plugin.Log.Info("Addon is null");
            return;
        }

        Plugin.Log.Info($"=== Addon Structure: {addon->NameString} ===");
        Plugin.Log.Info($"Node count: {addon->UldManager.NodeListCount}");
        Plugin.Log.Info($"Is visible: {addon->IsVisible}");

        for (int i = 0; i < addon->UldManager.NodeListCount && i < 50; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node != null)
            {
                PrintNode(node, 0, maxDepth, i);
            }
        }
    }

    private static void PrintNode(AtkResNode* node, int depth, int maxDepth, int index)
    {
        if (depth > maxDepth || node == null)
        {
            return;
        }

        var indent = new string(' ', depth * 2);
        Plugin.Log.Info($"{indent}[{index}] Type: {node->Type}, Visible: {node->IsVisible()}, " +
                       $"Pos: ({node->X}, {node->Y}), Size: ({node->Width}, {node->Height})");

        if (node->Type == NodeType.Text)
        {
            var textNode = node->GetAsAtkTextNode();
            if (textNode != null)
            {
                Plugin.Log.Info($"{indent}  Text: \"{textNode->NodeText.ToString()}\"");
            }
        }

        // Print children if it's a component
        if (node->Type >= NodeType.Res)
        {
            var componentNode = (AtkComponentNode*)node;
            if (componentNode != null && componentNode->Component != null)
            {
                var component = componentNode->Component;
                Plugin.Log.Info($"{indent}  Component: {component->UldManager.NodeListCount} child nodes");

                for (int i = 0; i < component->UldManager.NodeListCount && i < 20; i++)
                {
                    var childNode = component->UldManager.NodeList[i];
                    if (childNode != null)
                    {
                        PrintNode(childNode, depth + 1, maxDepth, i);
                    }
                }
            }
        }
    }
}
