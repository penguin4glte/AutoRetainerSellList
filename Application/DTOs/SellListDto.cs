namespace AutoRetainerSellList.Application.DTOs;

public record SellListDto(
    ulong RetainerId,
    string RetainerName,
    List<SellListItemDto> Items,
    int ItemCount,
    int RemainingSlots
);
