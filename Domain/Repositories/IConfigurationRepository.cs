using AutoRetainerSellList.Domain.Aggregates;
using AutoRetainerSellList.Domain.ValueObjects;

namespace AutoRetainerSellList.Domain.Repositories;

public interface IConfigurationRepository
{
    Task<Dictionary<RetainerId, SellListAggregate>> LoadAllSellListsAsync();
    Task<SellListAggregate?> GetSellListAsync(RetainerId retainerId);
    Task SaveSellListAsync(RetainerId retainerId, SellListAggregate aggregate);
    Task<bool> GetAutoExecuteEnabledAsync();
    Task SetAutoExecuteEnabledAsync(bool enabled);
    Task<string> GetChatLanguageAsync();
    Task SetChatLanguageAsync(string language);
    Task SaveAsync();
}
