using AutoRetainerSellList.Application.DTOs;
using AutoRetainerSellList.Domain.Repositories;
using ECommons.DalamudServices;

namespace AutoRetainerSellList.Application.Queries;

public class GetRetainerListQuery
{
    private readonly IRetainerRepository _retainerRepository;
    private readonly IConfigurationRepository _configRepository;

    public GetRetainerListQuery(IRetainerRepository retainerRepository, IConfigurationRepository configRepository)
    {
        _retainerRepository = retainerRepository;
        _configRepository = configRepository;
    }

    public async Task<List<RetainerDto>> ExecuteAsync()
    {
        try
        {
            var retainers = await _retainerRepository.GetAllAvailableRetainersAsync();
            var sellLists = await _configRepository.LoadAllSellListsAsync();

            var dtos = retainers.Select(r =>
            {
                var sellListItemCount = sellLists.ContainsKey(r.Id) ? sellLists[r.Id].ItemCount : 0;

                return new RetainerDto(
                    r.Id.Value,
                    r.Name.Value,
                    sellListItemCount,
                    20,
                    r.IsAvailable
                );
            }).ToList();

            return dtos;
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[GetRetainerListQuery] Error: {ex}");
            return new List<RetainerDto>();
        }
    }
}
