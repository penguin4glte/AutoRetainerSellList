using AutoRetainerSellList.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace AutoRetainerSellList.Tests.Domain.ValueObjects;

public class PriceTests
{
    [Fact]
    public void Constructor_WithValidValue_CreatesInstance()
    {
        // Arrange
        int validValue = 100;

        // Act
        var price = new Price(validValue);

        // Assert
        price.Value.Should().Be(validValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_WithInvalidValue_ThrowsArgumentException(int invalidValue)
    {
        // Act
        Action act = () => new Price(invalidValue);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Price must be at least 1 gil*");
    }

    [Fact]
    public void Constructor_WithMinimumValue_CreatesInstance()
    {
        // Arrange
        int minValue = 1;

        // Act
        var price = new Price(minValue);

        // Assert
        price.Value.Should().Be(minValue);
    }

    [Fact]
    public void ImplicitConversion_ToInt_ReturnsValue()
    {
        // Arrange
        var price = new Price(100);

        // Act
        int value = price;

        // Assert
        value.Should().Be(100);
    }

    [Fact]
    public void ImplicitConversion_FromInt_CreatesInstance()
    {
        // Arrange
        int value = 100;

        // Act
        Price price = value;

        // Assert
        price.Value.Should().Be(value);
    }

    [Fact]
    public void Equals_WithSameValue_ReturnsTrue()
    {
        // Arrange
        var price1 = new Price(100);
        var price2 = new Price(100);

        // Act & Assert
        price1.Should().Be(price2);
    }

    [Fact]
    public void Equals_WithDifferentValue_ReturnsFalse()
    {
        // Arrange
        var price1 = new Price(100);
        var price2 = new Price(200);

        // Act & Assert
        price1.Should().NotBe(price2);
    }

    [Fact]
    public void ToString_FormatsWithCommasAndGil()
    {
        // Arrange
        var price = new Price(1000000);

        // Act
        var result = price.ToString();

        // Assert
        result.Should().Be("1,000,000 gil");
    }
}
