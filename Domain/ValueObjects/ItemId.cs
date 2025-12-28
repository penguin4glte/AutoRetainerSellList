namespace AutoRetainerSellList.Domain.ValueObjects;

public sealed record ItemId
{
    public uint Value { get; }

    public ItemId(uint value)
    {
        if (value == 0)
            throw new ArgumentException("ItemId cannot be zero", nameof(value));

        Value = value;
    }

    public static implicit operator uint(ItemId itemId) => itemId.Value;
    public static implicit operator ItemId(uint value) => new(value);

    public override string ToString() => Value.ToString();
}
