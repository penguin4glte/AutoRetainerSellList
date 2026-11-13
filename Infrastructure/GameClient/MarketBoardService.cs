using AutoRetainerSellList.Domain.ValueObjects;
using AutoRetainerSellList.Domain.Services;
using Dalamud.Game.Network.Structures;
using ECommons.DalamudServices;

namespace AutoRetainerSellList.Infrastructure.GameClient;

/// <summary>
/// Collects market board offerings for the current "Compare Prices" request and calculates the selling price.
/// The server sends listings in batches of 10, so every batch received for the request is accumulated and
/// the price is only calculated once batches stop arriving.
/// </summary>
public class MarketBoardService : IDisposable
{
    // How long to wait without receiving a new batch before treating the listing set as complete
    private const int QuietPeriodMs = 300;

    private readonly object _lock = new();
    private readonly PricingStrategy _pricingStrategy;

    private ItemId? _currentRequestItemId;
    private bool _sellingHq;
    private bool _undercutHqOnly;
    private TaskCompletionSource<bool>? _firstBatchTcs;
    private readonly Dictionary<ulong, IMarketBoardItemListing> _listings = new();
    private readonly HashSet<int> _requestIds = new();
    private int _batchCount;
    private DateTime _requestStartedAt;
    private DateTime _lastBatchAt;

    public MarketBoardService(PricingStrategy pricingStrategy)
    {
        _pricingStrategy = pricingStrategy;
        Svc.MarketBoard.OfferingsReceived += OnMarketBoardOfferingsReceived;
    }

    public void BeginRequest(ItemId itemId, bool sellingHq, bool undercutHqOnly)
    {
        lock (_lock)
        {
            _currentRequestItemId = itemId;
            _sellingHq = sellingHq;
            _undercutHqOnly = undercutHqOnly;
            _firstBatchTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _listings.Clear();
            _requestIds.Clear();
            _batchCount = 0;
            _requestStartedAt = DateTime.UtcNow;
            _lastBatchAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Waits for the market board listings of the current request and returns the calculated selling price.
    /// Returns null on timeout, when there are no listings, or when no request was started for the item.
    /// </summary>
    public async Task<Price?> GetSellingPriceAsync(ItemId itemId, int timeoutMs = 10000)
    {
        TaskCompletionSource<bool>? tcs;
        lock (_lock)
        {
            if (_firstBatchTcs == null || _currentRequestItemId?.Value != itemId.Value)
            {
                Svc.Log.Error($"[MarketBoardService] No market board request in progress for item {itemId}");
                return null;
            }
            tcs = _firstBatchTcs;
        }

        try
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

            // Wait for the first batch
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
            if (completed != tcs.Task)
            {
                Svc.Log.Error($"[MarketBoardService] TIMEOUT waiting for market board price for item {itemId} after {timeoutMs}ms");
                return null;
            }

            // Wait until no new batch has arrived for QuietPeriodMs
            while (DateTime.UtcNow < deadline)
            {
                TimeSpan sinceLastBatch;
                lock (_lock)
                {
                    sinceLastBatch = DateTime.UtcNow - _lastBatchAt;
                }
                if (sinceLastBatch.TotalMilliseconds >= QuietPeriodMs)
                    break;
                await Task.Delay(50);
            }

            return CalculateSellingPrice(itemId);
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
                _firstBatchTcs = null;
                _currentRequestItemId = null;
                _listings.Clear();
                _requestIds.Clear();
            }
        }
    }

    private Price? CalculateSellingPrice(ItemId itemId)
    {
        List<IMarketBoardItemListing> listings;
        bool sellingHq, undercutHqOnly;
        int batchCount;
        double firstBatchDelayMs;
        string requestIds;
        lock (_lock)
        {
            listings = _listings.Values.ToList();
            sellingHq = _sellingHq;
            undercutHqOnly = _undercutHqOnly;
            batchCount = _batchCount;
            firstBatchDelayMs = (_lastBatchAt - _requestStartedAt).TotalMilliseconds;
            requestIds = string.Join(",", _requestIds);
        }

        Svc.Log.Debug(
            $"[MarketBoardService] Item {itemId}: received {listings.Count} listings in {batchCount} batches " +
            $"(requestIds: {requestIds}, last batch {firstBatchDelayMs:F0}ms after request): " +
            string.Join(", ", listings.OrderBy(l => l.PricePerUnit)
                .Select(l => $"{l.PricePerUnit}{(l.IsHq ? "HQ" : "")}({l.RetainerName})")));

        if (listings.Count == 0)
        {
            Svc.Log.Warning($"[MarketBoardService] No market board listings found for item {itemId}");
            return null;
        }

        var reference = _pricingStrategy.SelectReferenceListing(
            listings.Select(l => new MarketListing((int)Math.Clamp(l.PricePerUnit, 1u, (uint)int.MaxValue), l.IsHq)),
            sellingHq,
            undercutHqOnly);
        if (reference == null)
            return null;

        var sellingPrice = _pricingStrategy.CalculateSellingPrice(reference.Value.PricePerUnit);
        var referenceListing = listings.First(l =>
            (int)Math.Clamp(l.PricePerUnit, 1u, (uint)int.MaxValue) == reference.Value.PricePerUnit &&
            l.IsHq == reference.Value.IsHq);

        Svc.Log.Info(
            $"[MarketBoardService] Item {itemId} (selling HQ: {sellingHq}, HQ only: {undercutHqOnly}): " +
            $"reference {referenceListing.PricePerUnit} gil (retainer: {referenceListing.RetainerName}, HQ: {referenceListing.IsHq}), " +
            $"overall lowest {listings.Min(l => l.PricePerUnit)} gil, listings: {listings.Count} -> selling price {sellingPrice.Value} gil");

        return sellingPrice;
    }

    private void OnMarketBoardOfferingsReceived(IMarketBoardCurrentOfferings currentOfferings)
    {
        try
        {
            lock (_lock)
            {
                if (_firstBatchTcs == null || _currentRequestItemId == null)
                    return;

                if (currentOfferings.ItemListings.Count > 0)
                {
                    // Ignore offerings for a different item (e.g. a late packet from a previous request)
                    var receivedItemId = currentOfferings.ItemListings[0].ItemId % 1_000_000;
                    if (receivedItemId != _currentRequestItemId.Value % 1_000_000)
                    {
                        Svc.Log.Warning($"[MarketBoardService] Ignoring offerings for item {receivedItemId} (waiting for {_currentRequestItemId})");
                        return;
                    }

                    foreach (var listing in currentOfferings.ItemListings)
                        _listings[listing.ListingId] = listing;
                }

                _requestIds.Add(currentOfferings.RequestId);
                _batchCount++;
                _lastBatchAt = DateTime.UtcNow;
                _firstBatchTcs.TrySetResult(true);
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[MarketBoardService] Error processing market board offerings: {ex}");
        }
    }

    public void Dispose()
    {
        Svc.MarketBoard.OfferingsReceived -= OnMarketBoardOfferingsReceived;
        lock (_lock)
        {
            _firstBatchTcs?.TrySetCanceled();
            _firstBatchTcs = null;
        }
    }
}
