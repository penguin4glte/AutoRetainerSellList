namespace AutoRetainerSellList.Domain.ValueObjects;

public sealed record RetainerName
{
    public string Value { get; }

    public RetainerName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("RetainerName cannot be null or empty", nameof(value));

        Value = value;
    }

    public static implicit operator string(RetainerName name) => name.Value;
    public static implicit operator RetainerName(string value) => new(value);

    public override string ToString() => Value;
}
