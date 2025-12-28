using AutoRetainerSellList.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace AutoRetainerSellList.Tests.Domain.ValueObjects;

public class RetainerIdTests
{
    [Fact]
    public void Constructor_WithValidValue_CreatesInstance()
    {
        // Arrange
        ulong validValue = 12345;

        // Act
        var retainerId = new RetainerId(validValue);

        // Assert
        retainerId.Value.Should().Be(validValue);
    }

    [Fact]
    public void Constructor_WithZero_ThrowsArgumentException()
    {
        // Arrange
        ulong invalidValue = 0;

        // Act
        Action act = () => new RetainerId(invalidValue);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("RetainerId cannot be zero*");
    }

    [Fact]
    public void ImplicitConversion_ToUlong_ReturnsValue()
    {
        // Arrange
        var retainerId = new RetainerId(12345);

        // Act
        ulong value = retainerId;

        // Assert
        value.Should().Be(12345);
    }

    [Fact]
    public void ImplicitConversion_FromUlong_CreatesInstance()
    {
        // Arrange
        ulong value = 12345;

        // Act
        RetainerId retainerId = value;

        // Assert
        retainerId.Value.Should().Be(value);
    }

    [Fact]
    public void Equals_WithSameValue_ReturnsTrue()
    {
        // Arrange
        var retainerId1 = new RetainerId(12345);
        var retainerId2 = new RetainerId(12345);

        // Act & Assert
        retainerId1.Should().Be(retainerId2);
    }

    [Fact]
    public void Equals_WithDifferentValue_ReturnsFalse()
    {
        // Arrange
        var retainerId1 = new RetainerId(12345);
        var retainerId2 = new RetainerId(67890);

        // Act & Assert
        retainerId1.Should().NotBe(retainerId2);
    }

    [Fact]
    public void GetHashCode_WithSameValue_ReturnsSameHash()
    {
        // Arrange
        var retainerId1 = new RetainerId(12345);
        var retainerId2 = new RetainerId(12345);

        // Act & Assert
        retainerId1.GetHashCode().Should().Be(retainerId2.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsValueAsString()
    {
        // Arrange
        var retainerId = new RetainerId(12345);

        // Act
        var result = retainerId.ToString();

        // Assert
        result.Should().Be("12345");
    }
}
