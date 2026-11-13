using AutoRetainerSellList.Domain.ValueObjects;

namespace AutoRetainerSellList.Domain.Services;

/// <summary>A single market board listing as seen by the pricing logic.</summary>
public readonly record struct MarketListing(int PricePerUnit, bool IsHq);

public class PricingStrategy
{
    public Price CalculateSellingPrice(Price marketLowestPrice)
    {
        // Business rule: Undercut by 1 gil, but never go below 1 gil
        var calculatedPrice = Math.Max(marketLowestPrice.Value - 1, 1);
        return new Price(calculatedPrice);
    }

    public Price CalculateSellingPrice(int marketLowestPrice)
    {
        var calculatedPrice = Math.Max(marketLowestPrice - 1, 1);
        return new Price(calculatedPrice);
    }

    /// <summary>
    /// Picks the listing to undercut.
    /// When selling an HQ item with <paramref name="undercutHqOnly"/> enabled, only HQ listings are compared
    /// (falling back to all listings when no HQ listing exists). Otherwise the overall lowest listing is used.
    /// Returns null when there are no listings.
    /// </summary>
    public MarketListing? SelectReferenceListing(IEnumerable<MarketListing> listings, bool sellingHq, bool undercutHqOnly)
    {
        var all = listings.ToList();
        if (all.Count == 0)
            return null;

        if (sellingHq && undercutHqOnly)
        {
            var hqListings = all.Where(l => l.IsHq).ToList();
            if (hqListings.Count > 0)
                return hqListings.MinBy(l => l.PricePerUnit);
        }

        return all.MinBy(l => l.PricePerUnit);
    }
}
