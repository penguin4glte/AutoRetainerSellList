using AutoRetainerSellList.Application.DTOs;
using AutoRetainerSellList.Domain.Repositories;
using AutoRetainerSellList.Domain.ValueObjects;
using ECommons.DalamudServices;

namespace AutoRetainerSellList.Application.Queries;

public class GetSellListQuery
{
    private readonly IConfigurationRepository _configRepository;

    public GetSellListQuery(IConfigurationRepository configRepository)
    {
        _configRepository = configRepository;
    }

    public async Task<SellListDto?> ExecuteAsync(ulong retainerId)
    {
        try
        {
            var aggregate = await _configRepository.GetSellListAsync(new RetainerId(retainerId));
            if (aggregate == null)
                return null;

            var items = aggregate.Items.Select(i => new SellListItemDto(
                i.Id,
                i.ItemName,
                i.QuantityToMaintain,
                i.Guid
            )).ToList();

            return new SellListDto(
                aggregate.Retainer.Id.Value,
                aggregate.Retainer.Name.Value,
                items,
                aggregate.ItemCount,
                aggregate.RemainingSlots
            );
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[GetSellListQuery] Error: {ex}");
            return null;
        }
    }
}
