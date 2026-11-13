using AutoRetainerSellList.Domain.Services;
using AutoRetainerSellList.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace AutoRetainerSellList.Tests.Domain.Services;

public class PricingStrategyTests
{
    private readonly PricingStrategy _pricingStrategy;

    public PricingStrategyTests()
    {
        _pricingStrategy = new PricingStrategy();
    }

    [Theory]
    [InlineData(100, 99)]
    [InlineData(1000, 999)]
    [InlineData(5000, 4999)]
    [InlineData(999999, 999998)]
    public void CalculateSellingPrice_WithPriceObject_ReturnsLowestMinusOne(int marketPrice, int expectedPrice)
    {
        // Arrange
        var lowestPrice = new Price(marketPrice);

        // Act
        var result = _pricingStrategy.CalculateSellingPrice(lowestPrice);

        // Assert
        result.Value.Should().Be(expectedPrice);
    }

    [Theory]
    [InlineData(100, 99)]
    [InlineData(1000, 999)]
    [InlineData(5000, 4999)]
    public void CalculateSellingPrice_WithInt_ReturnsLowestMinusOne(int marketPrice, int expectedPrice)
    {
        // Act
        var result = _pricingStrategy.CalculateSellingPrice(marketPrice);

        // Assert
        result.Value.Should().Be(expectedPrice);
    }

    [Fact]
    public void CalculateSellingPrice_WithLowestPrice1_ReturnsMinimum1()
    {
        // Arrange - Market lowest is already 1 gil
        var lowestPrice = new Price(1);

        // Act
        var result = _pricingStrategy.CalculateSellingPrice(lowestPrice);

        // Assert - Cannot go below 1 gil
        result.Value.Should().Be(1);
    }

    [Fact]
    public void CalculateSellingPrice_WithLowestPrice2_Returns1()
    {
        // Arrange
        var lowestPrice = new Price(2);

        // Act
        var result = _pricingStrategy.CalculateSellingPrice(lowestPrice);

        // Assert
        result.Value.Should().Be(1);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public void CalculateSellingPrice_ResultIsValidPrice(int marketPrice)
    {
        // Arrange
        var lowestPrice = new Price(marketPrice);

        // Act
        var result = _pricingStrategy.CalculateSellingPrice(lowestPrice);

        // Assert - Result should be a valid Price (>= 1)
        result.Value.Should().BeGreaterOrEqualTo(1);
    }
}

public class PricingStrategySelectReferenceListingTests
{
    private readonly PricingStrategy _pricingStrategy = new();

    // Listings from the bug report screenshot (キャロットラペ)
    private static readonly MarketListing[] ReportedListings =
    {
        new(711, true),
        new(425, false),
        new(689, false),
        new(703, true),
        new(703, true),
        new(705, true),
    };

    [Fact]
    public void SelectReferenceListing_NoListings_ReturnsNull()
    {
        _pricingStrategy.SelectReferenceListing(Array.Empty<MarketListing>(), sellingHq: true, undercutHqOnly: true)
            .Should().BeNull();
    }

    [Fact]
    public void SelectReferenceListing_HqItemWithHqOnly_UsesLowestHqListing()
    {
        var result = _pricingStrategy.SelectReferenceListing(ReportedListings, sellingHq: true, undercutHqOnly: true);

        result.Should().Be(new MarketListing(703, true));
    }

    [Fact]
    public void SelectReferenceListing_HqItemWithoutHqOnly_UsesOverallLowest()
    {
        var result = _pricingStrategy.SelectReferenceListing(ReportedListings, sellingHq: true, undercutHqOnly: false);

        result.Should().Be(new MarketListing(425, false));
    }

    [Fact]
    public void SelectReferenceListing_NqItem_UsesOverallLowestEvenWithHqOnly()
    {
        var result = _pricingStrategy.SelectReferenceListing(ReportedListings, sellingHq: false, undercutHqOnly: true);

        result.Should().Be(new MarketListing(425, false));
    }

    [Fact]
    public void SelectReferenceListing_HqItemWithNoHqListings_FallsBackToOverallLowest()
    {
        var listings = new[] { new MarketListing(500, false), new MarketListing(450, false) };

        var result = _pricingStrategy.SelectReferenceListing(listings, sellingHq: true, undercutHqOnly: true);

        result.Should().Be(new MarketListing(450, false));
    }

    [Fact]
    public void SelectReferenceListing_DoesNotDependOnListingOrder()
    {
        var result = _pricingStrategy.SelectReferenceListing(ReportedListings.Reverse(), sellingHq: true, undercutHqOnly: true);

        result!.Value.PricePerUnit.Should().Be(703);
    }
}
