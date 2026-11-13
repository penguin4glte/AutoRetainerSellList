using AutoRetainerSellList.Domain.ValueObjects;
using AutoRetainerSellList.Domain.Services;
using Dalamud.Game.Network.Structures;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace AutoRetainerSellList.Infrastructure.GameClient;

/// <summary>
/// Calculates the selling price from the market board listings of the current "Compare Prices" request.
///
/// The listings are read from the game's own <see cref="InfoProxyItemSearch"/> (the same data the
/// Search Result window shows). Dalamud's OfferingsReceived event is only used as a freshness/activity
/// signal and for retainer names in logs, because it can miss some of the batches the server sends
/// (observed: 80 of 100 listings received, missing the cheapest ones).
///
/// All methods are expected to be called on the framework thread.
/// </summary>
public unsafe class MarketBoardService : IDisposable
{
    // How long the listing data must stay unchanged before treating it as complete
    private const int QuietPeriodMs = 300;
    // Accept the in-game data even if Dalamud never raised OfferingsReceived for the request
    private const int EventlessAcceptAfterMs = 3000;

    private readonly object _lock = new();
    private readonly PricingStrategy _pricingStrategy;

    private ItemId? _currentRequestItemId;
    private bool _sellingHq;
    private bool _undercutHqOnly;
    private int _eventBatchCount;
    private readonly Dictionary<ulong, string> _eventRetainerNames = new();
    private DateTime _requestStartedAt;
    private DateTime _lastChangeAt;
    private uint _lastSeenListingCount;
    private int _lastSeenBatchCount;
    private bool _lastSeenWaiting;

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
            _eventBatchCount = 0;
            _eventRetainerNames.Clear();
            _requestStartedAt = DateTime.UtcNow;
            _lastChangeAt = DateTime.UtcNow;
            _lastSeenListingCount = uint.MaxValue;
            _lastSeenBatchCount = -1;
            _lastSeenWaiting = true;
        }
    }

    /// <summary>
    /// Polls for the selling price of the current request. Call once per frame until it returns non-false.
    /// Returns false while the listings are still loading, true with <paramref name="sellingPrice"/> set when
    /// the price is ready, and null on failure (timeout, no listings, or no request in progress).
    /// </summary>
    public bool? TryGetSellingPrice(ItemId itemId, out Price? sellingPrice, int timeoutMs = 10000)
    {
        sellingPrice = null;
        try
        {
            lock (_lock)
            {
                if (_currentRequestItemId == null || _currentRequestItemId.Value != itemId.Value)
                {
                    Svc.Log.Error($"[MarketBoardService] No market board request in progress for item {itemId}");
                    return null;
                }

                var now = DateTime.UtcNow;
                var elapsedMs = (now - _requestStartedAt).TotalMilliseconds;

                var proxy = InfoProxyItemSearch.Instance();
                var proxyMatches = proxy != null && proxy->SearchItemId % 1_000_000 == itemId.Value % 1_000_000;
                var listingCount = proxyMatches ? proxy->ListingCount : 0;
                var waiting = !proxyMatches || proxy->WaitingForListings;

                if (listingCount != _lastSeenListingCount || _eventBatchCount != _lastSeenBatchCount || waiting != _lastSeenWaiting)
                {
                    _lastSeenListingCount = listingCount;
                    _lastSeenBatchCount = _eventBatchCount;
                    _lastSeenWaiting = waiting;
                    _lastChangeAt = now;
                }

                var fresh = _eventBatchCount > 0 || elapsedMs >= EventlessAcceptAfterMs;
                var stable = (now - _lastChangeAt).TotalMilliseconds >= QuietPeriodMs;

                if (!(proxyMatches && fresh && !waiting && stable))
                {
                    if (elapsedMs < timeoutMs)
                        return false;

                    Svc.Log.Error(
                        $"[MarketBoardService] TIMEOUT waiting for market board price for item {itemId} after {timeoutMs}ms " +
                        $"(proxy item: {(proxy != null ? proxy->SearchItemId : 0)}, waiting: {waiting}, listings: {listingCount}, event batches: {_eventBatchCount})");
                    _currentRequestItemId = null;
                    return null;
                }

                sellingPrice = CalculateSellingPrice(proxy, itemId);
                _currentRequestItemId = null;
                return sellingPrice != null ? true : null;
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[MarketBoardService] Error getting price: {ex}");
            lock (_lock)
            {
                _currentRequestItemId = null;
            }
            return null;
        }
    }

    // Caller must hold _lock
    private Price? CalculateSellingPrice(InfoProxyItemSearch* proxy, ItemId itemId)
    {
        var count = (int)Math.Min(proxy->ListingCount, (uint)proxy->Listings.Length);
        var listings = new List<(MarketListing Listing, string Seller)>(count);
        var missingFromEvent = 0;
        for (var i = 0; i < count; i++)
        {
            ref var l = ref proxy->Listings[i];
            if (l.ItemId % 1_000_000 != itemId.Value % 1_000_000)
                continue;

            if (!_eventRetainerNames.TryGetValue(l.ListingId, out var seller))
            {
                seller = $"retainer {l.RetainerId:X}, not in event";
                missingFromEvent++;
            }
            listings.Add((new MarketListing((int)Math.Clamp(l.UnitPrice, 1u, (uint)int.MaxValue), l.IsHqItem), seller));
        }

        Svc.Log.Debug(
            $"[MarketBoardService] Item {itemId}: {listings.Count} listings in game data " +
            $"({_eventBatchCount} event batches, {_eventRetainerNames.Count} listings via event, {missingFromEvent} missing from event, " +
            $"{(DateTime.UtcNow - _requestStartedAt).TotalMilliseconds:F0}ms after request): " +
            string.Join(", ", listings.OrderBy(l => l.Listing.PricePerUnit)
                .Select(l => $"{l.Listing.PricePerUnit}{(l.Listing.IsHq ? "HQ" : "")}({l.Seller})")));

        if (listings.Count == 0)
        {
            Svc.Log.Warning($"[MarketBoardService] No market board listings found for item {itemId}");
            return null;
        }

        var reference = _pricingStrategy.SelectReferenceListing(listings.Select(l => l.Listing), _sellingHq, _undercutHqOnly);
        if (reference == null)
            return null;

        var sellingPrice = _pricingStrategy.CalculateSellingPrice(reference.Value.PricePerUnit);
        var referenceSeller = listings.First(l => l.Listing == reference.Value).Seller;

        Svc.Log.Info(
            $"[MarketBoardService] Item {itemId} (selling HQ: {_sellingHq}, HQ only: {_undercutHqOnly}): " +
            $"reference {reference.Value.PricePerUnit} gil ({referenceSeller}, HQ: {reference.Value.IsHq}), " +
            $"overall lowest {listings.Min(l => l.Listing.PricePerUnit)} gil, listings: {listings.Count} -> selling price {sellingPrice.Value} gil");

        return sellingPrice;
    }

    private void OnMarketBoardOfferingsReceived(IMarketBoardCurrentOfferings currentOfferings)
    {
        try
        {
            lock (_lock)
            {
                if (_currentRequestItemId == null)
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
                        _eventRetainerNames[listing.ListingId] = listing.RetainerName;
                }

                _eventBatchCount++;
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
            _currentRequestItemId = null;
        }
    }
}
