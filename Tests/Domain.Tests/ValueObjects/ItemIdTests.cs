using AutoRetainerSellList.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace AutoRetainerSellList.Tests.Domain.ValueObjects;

public class ItemIdTests
{
    [Fact]
    public void Constructor_WithValidValue_CreatesInstance()
    {
        // Arrange
        uint validValue = 5333; // Example: Hi-Potion

        // Act
        var itemId = new ItemId(validValue);

        // Assert
        itemId.Value.Should().Be(validValue);
    }

    [Fact]
    public void Constructor_WithZero_ThrowsArgumentException()
    {
        // Arrange
        uint invalidValue = 0;

        // Act
        Action act = () => new ItemId(invalidValue);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("ItemId cannot be zero*");
    }

    [Fact]
    public void ImplicitConversion_ToUint_ReturnsValue()
    {
        // Arrange
        var itemId = new ItemId(5333);

        // Act
        uint value = itemId;

        // Assert
        value.Should().Be(5333);
    }

    [Fact]
    public void ImplicitConversion_FromUint_CreatesInstance()
    {
        // Arrange
        uint value = 5333;

        // Act
        ItemId itemId = value;

        // Assert
        itemId.Value.Should().Be(value);
    }

    [Fact]
    public void Equals_WithSameValue_ReturnsTrue()
    {
        // Arrange
        var itemId1 = new ItemId(5333);
        var itemId2 = new ItemId(5333);

        // Act & Assert
        itemId1.Should().Be(itemId2);
    }

    [Fact]
    public void Equals_WithDifferentValue_ReturnsFalse()
    {
        // Arrange
        var itemId1 = new ItemId(5333);
        var itemId2 = new ItemId(5334);

        // Act & Assert
        itemId1.Should().NotBe(itemId2);
    }

    [Fact]
    public void GetHashCode_WithSameValue_ReturnsSameHash()
    {
        // Arrange
        var itemId1 = new ItemId(5333);
        var itemId2 = new ItemId(5333);

        // Act & Assert
        itemId1.GetHashCode().Should().Be(itemId2.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsValueAsString()
    {
        // Arrange
        var itemId = new ItemId(5333);

        // Act
        var result = itemId.ToString();

        // Assert
        result.Should().Be("5333");
    }
}
