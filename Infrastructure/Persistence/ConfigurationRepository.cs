using AutoRetainerSellList.Domain.Aggregates;
using AutoRetainerSellList.Domain.Entities;
using AutoRetainerSellList.Domain.Repositories;
using AutoRetainerSellList.Domain.ValueObjects;
using Dalamud.Plugin;
using ECommons.DalamudServices;

namespace AutoRetainerSellList.Infrastructure.Persistence;

public class ConfigurationRepository : IConfigurationRepository
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private ConfigurationModel _config;

    public ConfigurationRepository(IDalamudPluginInterface pluginInterface)
    {
        _pluginInterface = pluginInterface;
        _config = _pluginInterface.GetPluginConfig() as ConfigurationModel ?? new ConfigurationModel();
    }

    public Task<Dictionary<RetainerId, SellListAggregate>> LoadAllSellListsAsync()
    {
        var result = new Dictionary<RetainerId, SellListAggregate>();

        foreach (var kvp in _config.RetainerSellLists)
        {
            var retainerId = new RetainerId(kvp.Key);
            var data = kvp.Value;

            var retainer = new Retainer(retainerId, new RetainerName(data.RetainerName));
            var items = data.Items.Select(itemData => new SellListItem(
                new ItemId(itemData.ItemId),
                itemData.ItemName,
                new Quantity(itemData.QuantityToMaintain),
                itemData.Guid
            )).ToList();

            var aggregate = new SellListAggregate(retainer, items);
            result[retainerId] = aggregate;
        }

        return Task.FromResult(result);
    }

    public Task<SellListAggregate?> GetSellListAsync(RetainerId retainerId)
    {
        if (!_config.RetainerSellLists.TryGetValue(retainerId.Value, out var data))
            return Task.FromResult<SellListAggregate?>(null);

        var retainer = new Retainer(retainerId, new RetainerName(data.RetainerName));
        var items = data.Items.Select(itemData => new SellListItem(
            new ItemId(itemData.ItemId),
            itemData.ItemName,
            new Quantity(itemData.QuantityToMaintain),
            itemData.Guid
        )).ToList();

        var aggregate = new SellListAggregate(retainer, items);
        return Task.FromResult<SellListAggregate?>(aggregate);
    }

    public Task SaveSellListAsync(RetainerId retainerId, SellListAggregate aggregate)
    {
        var data = new RetainerSellListData
        {
            RetainerId = retainerId.Value,
            RetainerName = aggregate.Retainer.Name.Value,
            Items = aggregate.Items.Select(item => new SellListItemData
            {
                ItemId = item.Id,
                ItemName = item.ItemName,
                QuantityToMaintain = item.QuantityToMaintain,
                Guid = item.Guid
            }).ToList()
        };

        _config.RetainerSellLists[retainerId.Value] = data;

        return Task.CompletedTask;
    }

    public Task<bool> GetAutoExecuteEnabledAsync()
    {
        return Task.FromResult(_config.AutoExecuteEnabled);
    }

    public Task SetAutoExecuteEnabledAsync(bool enabled)
    {
        _config.AutoExecuteEnabled = enabled;
        return Task.CompletedTask;
    }

    public Task SaveAsync()
    {
        try
        {
            _pluginInterface.SavePluginConfig(_config);
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Failed to save configuration: {ex}");
            throw;
        }

        return Task.CompletedTask;
    }
}
