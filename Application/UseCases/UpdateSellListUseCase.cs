using AutoRetainerSellList.Application.DTOs;
using AutoRetainerSellList.Domain.Aggregates;
using AutoRetainerSellList.Domain.Entities;
using AutoRetainerSellList.Domain.Repositories;
using AutoRetainerSellList.Domain.ValueObjects;
using ECommons.DalamudServices;

namespace AutoRetainerSellList.Application.UseCases;

public class UpdateSellListUseCase
{
    private readonly IConfigurationRepository _configRepository;

    public UpdateSellListUseCase(IConfigurationRepository configRepository)
    {
        _configRepository = configRepository;
    }

    public async Task<bool> ExecuteAsync(ulong retainerId, string retainerName, List<SellListItemDto> items)
    {
        try
        {
            var retainer = new Retainer(new RetainerId(retainerId), new RetainerName(retainerName));
            var domainItems = items.Select(dto => new SellListItem(
                new ItemId(dto.ItemId),
                dto.ItemName,
                new Quantity(dto.QuantityToMaintain),
                dto.Guid
            )).ToList();

            var aggregate = new SellListAggregate(retainer, domainItems);

            await _configRepository.SaveSellListAsync(new RetainerId(retainerId), aggregate);
            await _configRepository.SaveAsync();

            return true;
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[UpdateSellListUseCase] Error: {ex}");
            return false;
        }
    }
}
