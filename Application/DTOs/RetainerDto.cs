namespace AutoRetainerSellList.Application.DTOs;

public record RetainerDto(
    ulong RetainerId,
    string Name,
    int SellListItemCount,
    int MaxSellListItems,
    bool IsAvailable
);
