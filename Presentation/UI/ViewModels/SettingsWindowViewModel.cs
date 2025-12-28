using AutoRetainerSellList.Application.DTOs;
using AutoRetainerSellList.Application.Queries;
using AutoRetainerSellList.Application.UseCases;
using AutoRetainerSellList.Domain.ValueObjects;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;

namespace AutoRetainerSellList.Presentation.UI.ViewModels;

public class SettingsWindowViewModel
{
    private readonly GetRetainerListQuery _getRetainersQuery;
    private readonly GetSellListQuery _getSellListQuery;
    private readonly UpdateSellListUseCase _updateSellListUseCase;
    private readonly SearchItemsQuery _searchItemsQuery;

    public List<RetainerDto> Retainers { get; private set; } = new();
    public RetainerDto? SelectedRetainer { get; set; }
    public List<SellListItemDto> SellListItems { get; private set; } = new();

    // Item search
    public string SearchQuery { get; set; } = string.Empty;
    public List<SearchItemsQuery.ItemSearchResult> SearchResults { get; private set; } = new();
    public SearchItemsQuery.ItemSearchResult? SelectedSearchResult { get; set; }
    public int QuantityToAdd { get; set; } = 1;

    public SettingsWindowViewModel(
        GetRetainerListQuery getRetainersQuery,
        GetSellListQuery getSellListQuery,
        UpdateSellListUseCase updateSellListUseCase,
        SearchItemsQuery searchItemsQuery)
    {
        _getRetainersQuery = getRetainersQuery;
        _getSellListQuery = getSellListQuery;
        _updateSellListUseCase = updateSellListUseCase;
        _searchItemsQuery = searchItemsQuery;
    }

    public async void LoadRetainers()
    {
        try
        {
            Retainers = await _getRetainersQuery.ExecuteAsync();
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[SettingsWindowViewModel] Error loading retainers: {ex}");
        }
    }

    public async void LoadSellList(RetainerDto retainer)
    {
        try
        {
            SelectedRetainer = retainer;
            var sellList = await _getSellListQuery.ExecuteAsync(retainer.RetainerId);
            SellListItems = sellList?.Items ?? new List<SellListItemDto>();
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[SettingsWindowViewModel] Error loading sell list: {ex}");
        }
    }

    public void SearchItems(string query)
    {
        try
        {
            SearchQuery = query;
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                SearchResults = new List<SearchItemsQuery.ItemSearchResult>();
                return;
            }

            SearchResults = _searchItemsQuery.Execute(query);
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[SettingsWindowViewModel] Error searching items: {ex}");
        }
    }

    public async void AddItem(uint itemId, string itemName, int quantity)
    {
        try
        {
            if (SelectedRetainer == null)
            {
                Svc.Log.Warning("[SettingsWindowViewModel] No retainer selected");
                return;
            }

            var newItem = new SellListItemDto(
                itemId,
                itemName,
                quantity,
                System.Guid.NewGuid().ToString()
            );

            var updatedItems = SellListItems.ToList();
            updatedItems.Add(newItem);

            await _updateSellListUseCase.ExecuteAsync(
                SelectedRetainer.RetainerId,
                SelectedRetainer.Name,
                updatedItems);

            // Reload
            LoadSellList(SelectedRetainer);

        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[SettingsWindowViewModel] Error adding item: {ex}");
        }
    }

    public async void RemoveItem(string guid)
    {
        try
        {
            if (SelectedRetainer == null)
            {
                Svc.Log.Warning("[SettingsWindowViewModel] No retainer selected");
                return;
            }

            var updatedItems = SellListItems.Where(i => i.Guid != guid).ToList();

            await _updateSellListUseCase.ExecuteAsync(
                SelectedRetainer.RetainerId,
                SelectedRetainer.Name,
                updatedItems);

            // Reload
            LoadSellList(SelectedRetainer);

        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[SettingsWindowViewModel] Error removing item: {ex}");
        }
    }

    public async void ClearList()
    {
        try
        {
            if (SelectedRetainer == null)
            {
                Svc.Log.Warning("[SettingsWindowViewModel] No retainer selected");
                return;
            }

            await _updateSellListUseCase.ExecuteAsync(
                SelectedRetainer.RetainerId,
                SelectedRetainer.Name,
                new List<SellListItemDto>());

            // Reload
            LoadSellList(SelectedRetainer);

        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[SettingsWindowViewModel] Error clearing list: {ex}");
        }
    }

    public async void UpdateQuantity(string guid, int newQuantity)
    {
        try
        {
            if (SelectedRetainer == null)
            {
                Svc.Log.Warning("[SettingsWindowViewModel] No retainer selected");
                return;
            }

            var updatedItems = SellListItems.Select(item =>
            {
                if (item.Guid == guid)
                {
                    return new SellListItemDto(
                        item.ItemId,
                        item.ItemName,
                        newQuantity,
                        item.Guid
                    );
                }
                return item;
            }).ToList();

            await _updateSellListUseCase.ExecuteAsync(
                SelectedRetainer.RetainerId,
                SelectedRetainer.Name,
                updatedItems);

            // Reload
            LoadSellList(SelectedRetainer);

        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[SettingsWindowViewModel] Error updating quantity: {ex}");
        }
    }
}
