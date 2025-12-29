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

    // Change tracking
    private List<SellListItemDto> _originalSellListItems = new();
    public bool HasChanges { get; private set; } = false;

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
            SellListItems = sellList?.Items.ToList() ?? new List<SellListItemDto>();
            _originalSellListItems = sellList?.Items.ToList() ?? new List<SellListItemDto>();
            HasChanges = false;
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

    private void CheckForChanges()
    {
        // Compare current items with original items
        if (SellListItems.Count != _originalSellListItems.Count)
        {
            HasChanges = true;
            return;
        }

        for (int i = 0; i < SellListItems.Count; i++)
        {
            var current = SellListItems[i];
            var original = _originalSellListItems.FirstOrDefault(o => o.Guid == current.Guid);

            if (original == null ||
                current.ItemId != original.ItemId ||
                current.QuantityToMaintain != original.QuantityToMaintain)
            {
                HasChanges = true;
                return;
            }
        }

        HasChanges = false;
    }

    public async void SaveChanges()
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
                SellListItems);

            // Update original state after successful save
            _originalSellListItems = SellListItems.ToList();
            HasChanges = false;

            Svc.Log.Info($"[SettingsWindowViewModel] Changes saved for {SelectedRetainer.Name}");
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[SettingsWindowViewModel] Error saving changes: {ex}");
        }
    }

    public void CancelChanges()
    {
        try
        {
            if (SelectedRetainer == null)
            {
                Svc.Log.Warning("[SettingsWindowViewModel] No retainer selected");
                return;
            }

            // Restore original state
            SellListItems = _originalSellListItems.ToList();
            HasChanges = false;

            Svc.Log.Info($"[SettingsWindowViewModel] Changes cancelled for {SelectedRetainer.Name}");
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[SettingsWindowViewModel] Error cancelling changes: {ex}");
        }
    }

    public void AddItem(uint itemId, string itemName, int quantity)
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

            SellListItems.Add(newItem);
            CheckForChanges();
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[SettingsWindowViewModel] Error adding item: {ex}");
        }
    }

    public void RemoveItem(string guid)
    {
        try
        {
            if (SelectedRetainer == null)
            {
                Svc.Log.Warning("[SettingsWindowViewModel] No retainer selected");
                return;
            }

            var itemToRemove = SellListItems.FirstOrDefault(i => i.Guid == guid);
            if (itemToRemove != null)
            {
                SellListItems.Remove(itemToRemove);
                CheckForChanges();
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[SettingsWindowViewModel] Error removing item: {ex}");
        }
    }

    public void ClearList()
    {
        try
        {
            if (SelectedRetainer == null)
            {
                Svc.Log.Warning("[SettingsWindowViewModel] No retainer selected");
                return;
            }

            SellListItems.Clear();
            CheckForChanges();
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[SettingsWindowViewModel] Error clearing list: {ex}");
        }
    }

    public void UpdateQuantity(string guid, int newQuantity)
    {
        try
        {
            if (SelectedRetainer == null)
            {
                Svc.Log.Warning("[SettingsWindowViewModel] No retainer selected");
                return;
            }

            var itemIndex = SellListItems.FindIndex(i => i.Guid == guid);
            if (itemIndex >= 0)
            {
                var item = SellListItems[itemIndex];
                SellListItems[itemIndex] = new SellListItemDto(
                    item.ItemId,
                    item.ItemName,
                    newQuantity,
                    item.Guid
                );
                CheckForChanges();
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[SettingsWindowViewModel] Error updating quantity: {ex}");
        }
    }
}
