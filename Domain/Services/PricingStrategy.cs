using AutoRetainerSellList.Domain.ValueObjects;

namespace AutoRetainerSellList.Domain.Services;

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
}
