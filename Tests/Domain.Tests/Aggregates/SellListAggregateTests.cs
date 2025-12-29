using AutoRetainerSellList.Domain.Aggregates;
using AutoRetainerSellList.Domain.Entities;
using AutoRetainerSellList.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace AutoRetainerSellList.Tests.Domain.Aggregates;

public class SellListAggregateTests
{
    private readonly Retainer _testRetainer;

    public SellListAggregateTests()
    {
        _testRetainer = new Retainer(new RetainerId(12345), new RetainerName("TestRetainer"));
    }

    [Fact]
    public void Constructor_WithRetainer_CreatesEmptyList()
    {
        // Act
        var aggregate = new SellListAggregate(_testRetainer);

        // Assert
        aggregate.Retainer.Should().Be(_testRetainer);
        aggregate.Items.Should().BeEmpty();
        aggregate.ItemCount.Should().Be(0);
        aggregate.CanAddItem.Should().BeTrue();
        aggregate.RemainingSlots.Should().Be(20);
    }

    [Fact]
    public void Constructor_WithNullRetainer_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new SellListAggregate(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithItems_CreatesListWithItems()
    {
        // Arrange
        var items = new List<SellListItem>
        {
            new SellListItem(new ItemId(100), "Item1", new Quantity(5), Guid.NewGuid().ToString()),
            new SellListItem(new ItemId(200), "Item2", new Quantity(10), Guid.NewGuid().ToString())
        };

        // Act
        var aggregate = new SellListAggregate(_testRetainer, items);

        // Assert
        aggregate.Items.Should().HaveCount(2);
        aggregate.ItemCount.Should().Be(2);
    }

    [Fact]
    public void Constructor_WithMoreThan20Items_ThrowsInvalidOperationException()
    {
        // Arrange
        var items = Enumerable.Range(1, 21)
            .Select(i => new SellListItem(new ItemId((uint)i), $"Item{i}", new Quantity(1)))
            .ToList();

        // Act
        Action act = () => new SellListAggregate(_testRetainer, items);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot create sell list with more than 20 items");
    }

    [Fact]
    public void Constructor_WithDuplicateItems_ThrowsInvalidOperationException()
    {
        // Arrange
        var items = new List<SellListItem>
        {
            new SellListItem(new ItemId(100), "Item1", new Quantity(5), Guid.NewGuid().ToString()),
            new SellListItem(new ItemId(100), "Item1", new Quantity(10), Guid.NewGuid().ToString()) // Duplicate
        };

        // Act
        Action act = () => new SellListAggregate(_testRetainer, items);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Sell list contains duplicate items:*");
    }

    [Fact]
    public void AddItem_WithValidItem_AddsToList()
    {
        // Arrange
        var aggregate = new SellListAggregate(_testRetainer);

        // Act
        aggregate.AddItem(new ItemId(100), "TestItem", new Quantity(5));

        // Assert
        aggregate.ItemCount.Should().Be(1);
        ((uint)aggregate.Items[0].Id).Should().Be(100);
        aggregate.Items[0].ItemName.Should().Be("TestItem");
        ((int)aggregate.Items[0].QuantityToMaintain).Should().Be(5);
    }

    [Fact]
    public void AddItem_When20ItemsExist_ThrowsInvalidOperationException()
    {
        // Arrange
        var aggregate = new SellListAggregate(_testRetainer);
        for (int i = 1; i <= 20; i++)
        {
            aggregate.AddItem(new ItemId((uint)i), $"Item{i}", new Quantity(1));
        }

        // Act
        Action act = () => aggregate.AddItem(new ItemId(21), "Item21", new Quantity(1));

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot add more than 20 items to sell list");
    }

    [Fact]
    public void AddItem_WithDuplicateItemId_ThrowsInvalidOperationException()
    {
        // Arrange
        var aggregate = new SellListAggregate(_testRetainer);
        aggregate.AddItem(new ItemId(100), "Item1", new Quantity(5));

        // Act
        Action act = () => aggregate.AddItem(new ItemId(100), "Item1", new Quantity(10));

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Item * already exists in sell list");
    }

    [Fact]
    public void AddItem_WithSellListItemObject_AddsToList()
    {
        // Arrange
        var aggregate = new SellListAggregate(_testRetainer);
        var item = new SellListItem(new ItemId(100), "TestItem", new Quantity(5), Guid.NewGuid().ToString());

        // Act
        aggregate.AddItem(item);

        // Assert
        aggregate.ItemCount.Should().Be(1);
        aggregate.Items[0].Should().Be(item);
    }

    [Fact]
    public void RemoveItem_WithExistingItemId_RemovesFromList()
    {
        // Arrange
        var aggregate = new SellListAggregate(_testRetainer);
        aggregate.AddItem(new ItemId(100), "Item1", new Quantity(5));
        aggregate.AddItem(new ItemId(200), "Item2", new Quantity(10));

        // Act
        aggregate.RemoveItem(new ItemId(100));

        // Assert
        aggregate.ItemCount.Should().Be(1);
        ((uint)aggregate.Items[0].Id).Should().Be(200);
    }

    [Fact]
    public void RemoveItem_WithNonExistingItemId_ThrowsInvalidOperationException()
    {
        // Arrange
        var aggregate = new SellListAggregate(_testRetainer);

        // Act
        Action act = () => aggregate.RemoveItem(new ItemId(999));

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Item with ID 999 not found in sell list");
    }

    [Fact]
    public void RemoveItemByGuid_WithExistingGuid_RemovesFromList()
    {
        // Arrange
        var aggregate = new SellListAggregate(_testRetainer);
        aggregate.AddItem(new ItemId(100), "Item1", new Quantity(5));
        var guid = aggregate.Items[0].Guid;

        // Act
        aggregate.RemoveItemByGuid(guid);

        // Assert
        aggregate.ItemCount.Should().Be(0);
    }

    [Fact]
    public void UpdateQuantity_WithExistingItem_UpdatesQuantity()
    {
        // Arrange
        var aggregate = new SellListAggregate(_testRetainer);
        aggregate.AddItem(new ItemId(100), "Item1", new Quantity(5));

        // Act
        aggregate.UpdateQuantity(new ItemId(100), new Quantity(15));

        // Assert
        ((int)aggregate.Items[0].QuantityToMaintain).Should().Be(15);
    }

    [Fact]
    public void UpdateQuantity_WithNonExistingItem_ThrowsInvalidOperationException()
    {
        // Arrange
        var aggregate = new SellListAggregate(_testRetainer);

        // Act
        Action act = () => aggregate.UpdateQuantity(new ItemId(999), new Quantity(10));

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Item with ID 999 not found in sell list");
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        // Arrange
        var aggregate = new SellListAggregate(_testRetainer);
        aggregate.AddItem(new ItemId(100), "Item1", new Quantity(5));
        aggregate.AddItem(new ItemId(200), "Item2", new Quantity(10));

        // Act
        aggregate.Clear();

        // Assert
        aggregate.ItemCount.Should().Be(0);
        aggregate.Items.Should().BeEmpty();
    }

    [Fact]
    public void GetItem_WithExistingItemId_ReturnsItem()
    {
        // Arrange
        var aggregate = new SellListAggregate(_testRetainer);
        aggregate.AddItem(new ItemId(100), "Item1", new Quantity(5));

        // Act
        var item = aggregate.GetItem(new ItemId(100));

        // Assert
        item.Should().NotBeNull();
        item!.ItemName.Should().Be("Item1");
    }

    [Fact]
    public void GetItem_WithNonExistingItemId_ReturnsNull()
    {
        // Arrange
        var aggregate = new SellListAggregate(_testRetainer);

        // Act
        var item = aggregate.GetItem(new ItemId(999));

        // Assert
        item.Should().BeNull();
    }

    [Fact]
    public void ContainsItem_WithExistingItemId_ReturnsTrue()
    {
        // Arrange
        var aggregate = new SellListAggregate(_testRetainer);
        aggregate.AddItem(new ItemId(100), "Item1", new Quantity(5));

        // Act
        var result = aggregate.ContainsItem(new ItemId(100));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ContainsItem_WithNonExistingItemId_ReturnsFalse()
    {
        // Arrange
        var aggregate = new SellListAggregate(_testRetainer);

        // Act
        var result = aggregate.ContainsItem(new ItemId(999));

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanAddItem_WhenLessThan20Items_ReturnsTrue()
    {
        // Arrange
        var aggregate = new SellListAggregate(_testRetainer);
        aggregate.AddItem(new ItemId(100), "Item1", new Quantity(5));

        // Assert
        aggregate.CanAddItem.Should().BeTrue();
    }

    [Fact]
    public void CanAddItem_When20ItemsExist_ReturnsFalse()
    {
        // Arrange
        var aggregate = new SellListAggregate(_testRetainer);
        for (int i = 1; i <= 20; i++)
        {
            aggregate.AddItem(new ItemId((uint)i), $"Item{i}", new Quantity(1));
        }

        // Assert
        aggregate.CanAddItem.Should().BeFalse();
    }

    [Fact]
    public void RemainingSlots_ReturnsCorrectCount()
    {
        // Arrange
        var aggregate = new SellListAggregate(_testRetainer);
        aggregate.AddItem(new ItemId(100), "Item1", new Quantity(5));
        aggregate.AddItem(new ItemId(200), "Item2", new Quantity(10));
        aggregate.AddItem(new ItemId(300), "Item3", new Quantity(15));

        // Assert
        aggregate.RemainingSlots.Should().Be(17);
    }

    [Fact]
    public void IsEmpty_Should_Return_True_When_No_Items()
    {
        // Arrange
        var aggregate = new SellListAggregate(_testRetainer);

        // Assert
        aggregate.IsEmpty.Should().BeTrue();
        aggregate.ItemCount.Should().Be(0);
    }

    [Fact]
    public void IsEmpty_Should_Return_False_When_Items_Exist()
    {
        // Arrange
        var aggregate = new SellListAggregate(_testRetainer);
        aggregate.AddItem(new ItemId(100), "Item1", new Quantity(5));

        // Assert
        aggregate.IsEmpty.Should().BeFalse();
        aggregate.ItemCount.Should().Be(1);
    }
}
