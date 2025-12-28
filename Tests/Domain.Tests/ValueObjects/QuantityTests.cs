using AutoRetainerSellList.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace AutoRetainerSellList.Tests.Domain.ValueObjects;

public class QuantityTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(500)]
    [InlineData(999)]
    public void Constructor_WithValidValue_CreatesInstance(int validValue)
    {
        // Act
        var quantity = new Quantity(validValue);

        // Assert
        quantity.Value.Should().Be(validValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1000)]
    [InlineData(2000)]
    public void Constructor_WithInvalidValue_ThrowsArgumentException(int invalidValue)
    {
        // Act
        Action act = () => new Quantity(invalidValue);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Quantity must be between 1 and 999*");
    }

    [Fact]
    public void ImplicitConversion_ToInt_ReturnsValue()
    {
        // Arrange
        var quantity = new Quantity(50);

        // Act
        int value = quantity;

        // Assert
        value.Should().Be(50);
    }

    [Fact]
    public void ImplicitConversion_FromInt_CreatesInstance()
    {
        // Arrange
        int value = 50;

        // Act
        Quantity quantity = value;

        // Assert
        quantity.Value.Should().Be(value);
    }

    [Fact]
    public void Equals_WithSameValue_ReturnsTrue()
    {
        // Arrange
        var quantity1 = new Quantity(50);
        var quantity2 = new Quantity(50);

        // Act & Assert
        quantity1.Should().Be(quantity2);
    }

    [Fact]
    public void ToString_ReturnsValueAsString()
    {
        // Arrange
        var quantity = new Quantity(50);

        // Act
        var result = quantity.ToString();

        // Assert
        result.Should().Be("50");
    }
}
