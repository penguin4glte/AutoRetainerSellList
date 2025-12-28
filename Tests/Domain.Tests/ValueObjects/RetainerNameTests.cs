using AutoRetainerSellList.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace AutoRetainerSellList.Tests.Domain.ValueObjects;

public class RetainerNameTests
{
    [Theory]
    [InlineData("MyRetainer")]
    [InlineData("リテイナー1")]
    [InlineData("Retainer with spaces")]
    public void Constructor_WithValidValue_CreatesInstance(string validName)
    {
        // Act
        var retainerName = new RetainerName(validName);

        // Assert
        retainerName.Value.Should().Be(validName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Constructor_WithInvalidValue_ThrowsArgumentException(string? invalidName)
    {
        // Act
        Action act = () => new RetainerName(invalidName!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("RetainerName cannot be null or empty*");
    }

    [Fact]
    public void ImplicitConversion_ToString_ReturnsValue()
    {
        // Arrange
        var retainerName = new RetainerName("MyRetainer");

        // Act
        string value = retainerName;

        // Assert
        value.Should().Be("MyRetainer");
    }

    [Fact]
    public void ImplicitConversion_FromString_CreatesInstance()
    {
        // Arrange
        string value = "MyRetainer";

        // Act
        RetainerName retainerName = value;

        // Assert
        retainerName.Value.Should().Be(value);
    }

    [Fact]
    public void Equals_WithSameValue_ReturnsTrue()
    {
        // Arrange
        var retainerName1 = new RetainerName("MyRetainer");
        var retainerName2 = new RetainerName("MyRetainer");

        // Act & Assert
        retainerName1.Should().Be(retainerName2);
    }

    [Fact]
    public void Equals_WithDifferentValue_ReturnsFalse()
    {
        // Arrange
        var retainerName1 = new RetainerName("MyRetainer");
        var retainerName2 = new RetainerName("OtherRetainer");

        // Act & Assert
        retainerName1.Should().NotBe(retainerName2);
    }

    [Fact]
    public void GetHashCode_WithSameValue_ReturnsSameHash()
    {
        // Arrange
        var retainerName1 = new RetainerName("MyRetainer");
        var retainerName2 = new RetainerName("MyRetainer");

        // Act & Assert
        retainerName1.GetHashCode().Should().Be(retainerName2.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        // Arrange
        var retainerName = new RetainerName("MyRetainer");

        // Act
        var result = retainerName.ToString();

        // Assert
        result.Should().Be("MyRetainer");
    }
}
