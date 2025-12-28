using AutoRetainerSellList.Domain.ValueObjects;

namespace AutoRetainerSellList.Domain.Entities;

public class Retainer
{
    public RetainerId Id { get; }
    public RetainerName Name { get; private set; }
    public bool IsAvailable { get; }

    public Retainer(RetainerId id, RetainerName name, bool isAvailable = true)
    {
        Id = id;
        Name = name;
        IsAvailable = isAvailable;
    }

    public void UpdateName(RetainerName newName)
    {
        Name = newName;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Retainer other) return false;
        return Id.Equals(other.Id);
    }

    public override int GetHashCode() => Id.GetHashCode();

    public override string ToString() => $"{Name} (ID: {Id})";
}
