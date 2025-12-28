namespace AutoRetainerSellList.Domain.ValueObjects;

public sealed record Price
{
    public int Value { get; }

    public Price(int value)
    {
        if (value < 1)
            throw new ArgumentException("Price must be at least 1 gil", nameof(value));

        Value = value;
    }

    public static implicit operator int(Price price) => price.Value;
    public static implicit operator Price(int value) => new(value);

    public override string ToString() => $"{Value:N0} gil";
}
