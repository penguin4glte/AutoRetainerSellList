namespace AutoRetainerSellList.Application.DTOs;

public record SellListItemDto(
    uint ItemId,
    string ItemName,
    int QuantityToMaintain,
    string Guid
);
