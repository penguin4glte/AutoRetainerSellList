using AutoRetainerSellList.Domain.Entities;
using AutoRetainerSellList.Domain.ValueObjects;

namespace AutoRetainerSellList.Domain.Aggregates;

public class SellListAggregate
{
    private readonly List<SellListItem> _items;

    public const int MaxItems = 20;

    public Retainer Retainer { get; }
    public IReadOnlyList<SellListItem> Items => _items.AsReadOnly();
    public int ItemCount => _items.Count;
    public bool CanAddItem => _items.Count < MaxItems;
    public int RemainingSlots => MaxItems - _items.Count;

    public SellListAggregate(Retainer retainer)
    {
        Retainer = retainer ?? throw new ArgumentNullException(nameof(retainer));
        _items = new List<SellListItem>();
    }

    public SellListAggregate(Retainer retainer, IEnumerable<SellListItem> items)
    {
        Retainer = retainer ?? throw new ArgumentNullException(nameof(retainer));
        _items = new List<SellListItem>(items ?? throw new ArgumentNullException(nameof(items)));

        if (_items.Count > MaxItems)
            throw new InvalidOperationException($"Cannot create sell list with more than {MaxItems} items");

        // Check for duplicates
        var duplicates = _items.GroupBy(x => x.Id).Where(g => g.Count() > 1).ToList();
        if (duplicates.Any())
            throw new InvalidOperationException($"Sell list contains duplicate items: {string.Join(", ", duplicates.Select(g => g.Key))}");
    }

    public void AddItem(ItemId itemId, string itemName, Quantity quantityToMaintain)
    {
        if (_items.Count >= MaxItems)
            throw new InvalidOperationException($"Cannot add more than {MaxItems} items to sell list");

        if (_items.Any(x => x.Id == itemId))
            throw new InvalidOperationException($"Item {itemName} (ID: {itemId}) already exists in sell list");

        var item = new SellListItem(itemId, itemName, quantityToMaintain, System.Guid.NewGuid().ToString());
        _items.Add(item);
    }

    public void AddItem(SellListItem item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        if (_items.Count >= MaxItems)
            throw new InvalidOperationException($"Cannot add more than {MaxItems} items to sell list");

        if (_items.Any(x => x.Id == item.Id))
            throw new InvalidOperationException($"Item {item.ItemName} (ID: {item.Id}) already exists in sell list");

        _items.Add(item);
    }

    public void RemoveItem(ItemId itemId)
    {
        var item = _items.FirstOrDefault(x => x.Id == itemId);
        if (item == null)
            throw new InvalidOperationException($"Item with ID {itemId} not found in sell list");

        _items.Remove(item);
    }

    public void RemoveItemByGuid(string guid)
    {
        var item = _items.FirstOrDefault(x => x.Guid == guid);
        if (item == null)
            throw new InvalidOperationException($"Item with GUID {guid} not found in sell list");

        _items.Remove(item);
    }

    public void UpdateQuantity(ItemId itemId, Quantity newQuantity)
    {
        var item = _items.FirstOrDefault(x => x.Id == itemId);
        if (item == null)
            throw new InvalidOperationException($"Item with ID {itemId} not found in sell list");

        // Remove and re-add with new quantity
        _items.Remove(item);
        var newItem = new SellListItem(item.Id, item.ItemName, newQuantity, item.Guid);
        _items.Add(newItem);
    }

    public void Clear()
    {
        _items.Clear();
    }

    public SellListItem? GetItem(ItemId itemId)
    {
        return _items.FirstOrDefault(x => x.Id == itemId);
    }

    public bool ContainsItem(ItemId itemId)
    {
        return _items.Any(x => x.Id == itemId);
    }

    public override string ToString() => $"SellList for {Retainer.Name}: {ItemCount}/{MaxItems} items";
}
