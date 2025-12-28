namespace AutoRetainerSellList.Domain.ValueObjects;

public sealed record Quantity
{
    public int Value { get; }

    public Quantity(int value)
    {
        if (value < 1 || value > 999)
            throw new ArgumentException("Quantity must be between 1 and 999", nameof(value));

        Value = value;
    }

    public static implicit operator int(Quantity quantity) => quantity.Value;
    public static implicit operator Quantity(int value) => new(value);

    public override string ToString() => Value.ToString();
}
