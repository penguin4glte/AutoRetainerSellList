using AutoRetainerSellList.Domain.ValueObjects;
using AutoRetainerSellList.Domain.Services;
using Dalamud.Game.Network.Structures;
using ECommons.DalamudServices;

namespace AutoRetainerSellList.Infrastructure.GameClient;

public class MarketBoardService : IDisposable
{
    private TaskCompletionSource<Price>? _priceRequestTcs;
    private int _lastRequestId = -1;
    private readonly object _lock = new();
    private ItemId? _currentRequestItemId;
    private readonly PricingStrategy _pricingStrategy;

    public MarketBoardService(PricingStrategy pricingStrategy)
    {
        _pricingStrategy = pricingStrategy;
        Svc.MarketBoard.OfferingsReceived += OnMarketBoardOfferingsReceived;
    }

    public void BeginRequest(ItemId itemId)
    {
        lock (_lock)
        {
            _currentRequestItemId = itemId;
            _priceRequestTcs = new TaskCompletionSource<Price>();
        }
    }

    public async Task<Price?> GetLowestPriceAsync(ItemId itemId, int timeoutMs = 10000)
    {
        TaskCompletionSource<Price>? tcs;
        lock (_lock)
        {
            // If BeginRequest was not called, create TCS now
            if (_priceRequestTcs == null || _currentRequestItemId?.Value != itemId.Value)
            {
                Svc.Log.Warning($"[MarketBoardService] BeginRequest was not called for item {itemId}, creating TCS now");
                _currentRequestItemId = itemId;
                _priceRequestTcs = new TaskCompletionSource<Price>();
            }
            tcs = _priceRequestTcs;
        }

        try
        {

            using var cts = new CancellationTokenSource(timeoutMs);
            cts.Token.Register(() =>
            {
                lock (_lock)
                {
                    _priceRequestTcs?.TrySetCanceled();
                }
            });

            var price = await tcs.Task;
            return price;
        }
        catch (OperationCanceledException)
        {
            Svc.Log.Error($"[MarketBoardService] TIMEOUT waiting for market board price for item {itemId} after {timeoutMs}ms");
            return null;
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[MarketBoardService] Error getting price: {ex}");
            return null;
        }
        finally
        {
            lock (_lock)
            {
                _priceRequestTcs = null;
                _currentRequestItemId = null;
            }
        }
    }

    private void OnMarketBoardOfferingsReceived(IMarketBoardCurrentOfferings currentOfferings)
    {
        try
        {
            ItemId? requestedItemId;
            lock (_lock)
            {
                if (_priceRequestTcs == null || _priceRequestTcs.Task.IsCompleted)
                    return;
                requestedItemId = _currentRequestItemId;
            }

            // Ignore offerings for a different item (e.g. a late packet from a previous request)
            if (requestedItemId != null && currentOfferings.ItemListings.Count > 0)
            {
                var receivedItemId = currentOfferings.ItemListings[0].ItemId % 1_000_000;
                if (receivedItemId != requestedItemId.Value % 1_000_000)
                {
                    Svc.Log.Warning($"[MarketBoardService] Ignoring offerings for item {receivedItemId} (waiting for {requestedItemId})");
                    return;
                }
            }

            if (currentOfferings.ItemListings.Count == 0)
            {
                Svc.Log.Warning("[MarketBoardService] No market board listings found");
                lock (_lock)
                {
                    // No listings = cannot determine safe price, return null
                    _priceRequestTcs?.TrySetResult(null!);
                }
                return;
            }

            // Check for duplicate request
            if (currentOfferings.RequestId == _lastRequestId)
            {
                return;
            }

            _lastRequestId = currentOfferings.RequestId;

            // Get lowest market price (do not rely on listing order)
            var lowestListing = currentOfferings.ItemListings.MinBy(l => l.PricePerUnit)!;
            var marketPrice = new Price((int)Math.Clamp(lowestListing.PricePerUnit, 1u, (uint)int.MaxValue));

            // Calculate selling price using PricingStrategy
            var sellingPrice = _pricingStrategy.CalculateSellingPrice(marketPrice);

            Svc.Log.Info(
                $"[MarketBoardService] Item {lowestListing.ItemId}: lowest {lowestListing.PricePerUnit} gil " +
                $"(retainer: {lowestListing.RetainerName}, HQ: {lowestListing.IsHq}, listings: {currentOfferings.ItemListings.Count}, " +
                $"requestId: {currentOfferings.RequestId}) -> selling price {sellingPrice.Value} gil");


            lock (_lock)
            {
                _priceRequestTcs?.TrySetResult(sellingPrice);
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[MarketBoardService] Error processing market board offerings: {ex}");
            lock (_lock)
            {
                _priceRequestTcs?.TrySetException(ex);
            }
        }
    }

    public void Dispose()
    {
        Svc.MarketBoard.OfferingsReceived -= OnMarketBoardOfferingsReceived;
        lock (_lock)
        {
            _priceRequestTcs?.TrySetCanceled();
            _priceRequestTcs = null;
        }
    }
}
