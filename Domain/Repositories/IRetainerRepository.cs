using AutoRetainerSellList.Domain.Entities;
using AutoRetainerSellList.Domain.ValueObjects;

namespace AutoRetainerSellList.Domain.Repositories;

public interface IRetainerRepository
{
    Task<List<Retainer>> GetAllAvailableRetainersAsync();
    Task<Retainer?> GetRetainerByIdAsync(RetainerId id);
    Task<int> GetCurrentListedCountAsync(RetainerId retainerId, ItemId itemId);
    Task<InventorySlot?> FindItemInInventoryAsync(RetainerId retainerId, ItemId itemId);
}

public record InventorySlot(
    string InventoryType,
    uint SlotIndex,
    int Quantity
);
