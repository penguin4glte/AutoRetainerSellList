namespace AutoRetainerSellList.Domain.ValueObjects;

public sealed record RetainerId
{
    public ulong Value { get; }

    public RetainerId(ulong value)
    {
        if (value == 0)
            throw new ArgumentException("RetainerId cannot be zero", nameof(value));

        Value = value;
    }

    public static implicit operator ulong(RetainerId retainerId) => retainerId.Value;
    public static implicit operator RetainerId(ulong value) => new(value);

    public override string ToString() => Value.ToString();
}
