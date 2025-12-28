using AutoRetainerSellList.Domain.Repositories;
using AutoRetainerSellList.Domain.ValueObjects;
using AutoRetainerSellList.Infrastructure.Automation;
using AutoRetainerSellList.Infrastructure.GameClient;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoRetainerSellList.Application.UseCases;

public class ExecuteSellListUseCase
{
    private readonly IConfigurationRepository _configRepository;
    private readonly IRetainerRepository _retainerRepository;
    private readonly GameUIService _gameUIService;
    private readonly MarketBoardService _marketBoardService;
    private readonly TaskExecutor _taskExecutor;

    public bool IsRunning => _taskExecutor.IsBusy;

    public ExecuteSellListUseCase(
        IConfigurationRepository configRepository,
        IRetainerRepository retainerRepository,
        GameUIService gameUIService,
        MarketBoardService marketBoardService,
        TaskExecutor taskExecutor)
    {
        _configRepository = configRepository;
        _retainerRepository = retainerRepository;
        _gameUIService = gameUIService;
        _marketBoardService = marketBoardService;
        _taskExecutor = taskExecutor;
    }

    public async Task<bool> ExecuteAsync(ulong retainerId, Action? onComplete = null)
    {
        try
        {

            var aggregate = await _configRepository.GetSellListAsync(new RetainerId(retainerId));
            if (aggregate == null)
            {
                Svc.Log.Warning($"[ExecuteSellListUseCase] No sell list found for retainer {retainerId}");
                onComplete?.Invoke();
                return false;
            }

            if (aggregate.ItemCount == 0)
            {
                onComplete?.Invoke();
                return true;
            }

            // Enqueue tasks for each item
            foreach (var item in aggregate.Items)
            {
                EnqueueSellItem(retainerId, item.Id, item.ItemName, item.QuantityToMaintain);
            }

            // Enqueue completion callback as the last task
            if (onComplete != null)
            {
                _taskExecutor.Enqueue(() =>
                {
                    onComplete.Invoke();
                    return true;
                }, "CompletionCallback");
            }

            return true;
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[ExecuteSellListUseCase] Error: {ex}");
            onComplete?.Invoke();
            return false;
        }
    }

    private void EnqueueSellItem(ulong retainerId, ItemId itemId, string itemName, Quantity quantity)
    {
        _taskExecutor.Enqueue(() =>
        {
            var result = ProcessSellItemAsync(retainerId, itemId, itemName, quantity).GetAwaiter().GetResult();
            return result;
        }, $"ProcessItem_{itemId}");
    }

    private async Task<bool?> ProcessSellItemAsync(ulong retainerId, ItemId itemId, string itemName, Quantity quantity)
    {
        try
        {

            // Check current listed count
            var currentListed = await _retainerRepository.GetCurrentListedCountAsync(new RetainerId(retainerId), itemId);
            var needed = quantity.Value - currentListed;

            if (needed <= 0)
            {
                _gameUIService.PrintItemMessage(itemId.Value, $" はすでに {currentListed}/{quantity} 出品済みです");
                return true;
            }

            // Find item in inventory
            var inventorySlot = await _retainerRepository.FindItemInInventoryAsync(new RetainerId(retainerId), itemId);
            if (inventorySlot == null)
            {
                Svc.Log.Warning($"[ExecuteSellListUseCase] {itemName} not found in retainer inventory");
                _gameUIService.PrintErrorMessage(itemId.Value, " がリテイナー所持品に見つかりません");
                return true;
            }

            // Convert inventory type string back to enum
            if (!Enum.TryParse<InventoryType>(inventorySlot.InventoryType, out var inventoryType))
            {
                Svc.Log.Error($"[ExecuteSellListUseCase] Invalid inventory type: {inventorySlot.InventoryType}");
                return true;
            }

            // Enqueue UI interaction tasks
            SellItemFromSlot(inventoryType, inventorySlot.SlotIndex, itemId, itemName);

            return true;
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[ExecuteSellListUseCase] Error in ProcessSellItemAsync: {ex}");
            return true; // Continue with next item
        }
    }

    private void SellItemFromSlot(InventoryType inventoryType, uint slot, ItemId itemId, string itemName)
    {
        // Open context menu
        _taskExecutor.Enqueue(() => _gameUIService.OpenItemContextMenu(inventoryType, slot), $"OpenMenu_{itemId}");
        _taskExecutor.EnqueueDelay(200);

        // Click "Put Up for Sale"
        _taskExecutor.Enqueue(() => _gameUIService.ClickPutUpForSale(), $"ClickSale_{itemId}");
        _taskExecutor.EnqueueDelay(200);

        // Wait for RetainerSell addon
        _taskExecutor.Enqueue(() => _gameUIService.WaitForRetainerSell(), $"WaitSell_{itemId}");
        _taskExecutor.EnqueueDelay(100);

        // Request market board price (BeginRequest is called inside this method)
        _taskExecutor.Enqueue(() => _gameUIService.RequestMarketBoardPrice(itemId), $"RequestPrice_{itemId}");
        _taskExecutor.EnqueueDelay(1500); // Wait for market board data (price calculation happens in MarketBoardService)

        // Set price and confirm
        _taskExecutor.Enqueue(() =>
        {
            return SetPriceAndConfirmAsync(itemId, itemName).GetAwaiter().GetResult();
        }, $"SetPrice_{itemId}");
        _taskExecutor.EnqueueDelay(500);
    }

    private async Task<bool?> SetPriceAndConfirmAsync(ItemId itemId, string itemName)
    {
        try
        {
            // GetLowestPriceAsync now returns the calculated selling price (not market price)
            var sellingPrice = await _marketBoardService.GetLowestPriceAsync(itemId, timeoutMs: 10000);
            if (sellingPrice == null)
            {
                Svc.Log.Error($"[ExecuteSellListUseCase] Failed to get price for {itemName}, CANCELING (will not list at dangerous price)");
                _gameUIService.PrintErrorMessage(itemId.Value, " の価格取得に失敗しました。出品をキャンセルします。");
                return _gameUIService.CancelRetainerSell();
            }

            return _gameUIService.SetPriceAndConfirm(sellingPrice, itemId, itemName);
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[ExecuteSellListUseCase] Error in SetPriceAndConfirmAsync: {ex}");
            _gameUIService.PrintErrorMessage(itemId.Value, " の処理中にエラーが発生しました。");
            return _gameUIService.CancelRetainerSell();
        }
    }

    public void Abort()
    {
        _taskExecutor.Abort();
    }
}
