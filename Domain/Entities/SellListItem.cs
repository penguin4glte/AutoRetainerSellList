using AutoRetainerSellList.Domain.ValueObjects;

namespace AutoRetainerSellList.Domain.Entities;

public class SellListItem
{
    public ItemId Id { get; }
    public string ItemName { get; private set; }
    public Quantity QuantityToMaintain { get; private set; }
    public string Guid { get; }

    public SellListItem(ItemId itemId, string itemName, Quantity quantityToMaintain)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            throw new ArgumentException("ItemName cannot be null or empty", nameof(itemName));

        Id = itemId;
        ItemName = itemName;
        QuantityToMaintain = quantityToMaintain;
        Guid = System.Guid.NewGuid().ToString();
    }

    public SellListItem(ItemId itemId, string itemName, Quantity quantityToMaintain, string guid)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            throw new ArgumentException("ItemName cannot be null or empty", nameof(itemName));
        if (string.IsNullOrWhiteSpace(guid))
            throw new ArgumentException("Guid cannot be null or empty", nameof(guid));

        Id = itemId;
        ItemName = itemName;
        QuantityToMaintain = quantityToMaintain;
        Guid = guid;
    }

    public void UpdateQuantity(Quantity newQuantity)
    {
        QuantityToMaintain = newQuantity;
    }

    public void UpdateItemName(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("ItemName cannot be null or empty", nameof(newName));

        ItemName = newName;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not SellListItem other) return false;
        return Id.Equals(other.Id);
    }

    public override int GetHashCode() => Id.GetHashCode();

    public override string ToString() => $"{ItemName} x{QuantityToMaintain}";
}
