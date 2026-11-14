using AutoRetainerSellList.Domain.ValueObjects;
using AutoRetainerSellList.Domain.Services;
using AutoRetainerSellList.Infrastructure.Monitoring;
using Dalamud.Game.Network.Structures;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace AutoRetainerSellList.Infrastructure.GameClient;

/// <summary>
/// Calculates the selling price from the market board listings of the current "Compare Prices" request.
///
/// The listings are read from the game's own <see cref="InfoProxyItemSearch"/> (the same data the
/// Search Result window shows). Observed behavior of its fields:
/// - ListingCount is the total number of listings, set when the first batch of a request arrives.
/// - EntryCount is the number of listings received so far (+10 per batch).
/// - Sending another Compare Prices request while one is in flight (e.g. Marketbuddy's auto Compare Prices
///   followed by ours) resets both to 0, and the response to the second request can be delayed (~2s) or
///   never arrive. The slots keep the first request's listings, so they are read directly in that case.
///
/// Dalamud's OfferingsReceived event is only used as a freshness/activity signal and for retainer names in
/// logs: batches that arrive before <see cref="BeginRequest"/> (e.g. from another plugin's request) are not
/// seen, so it can miss the cheapest listings.
///
/// All methods are expected to be called on the framework thread.
/// </summary>
public unsafe class MarketBoardService : IDisposable
{
    // How long the listing data must stay unchanged before treating it as complete
    private const int QuietPeriodMs = 300;

    private readonly object _lock = new();
    private readonly PricingStrategy _pricingStrategy;

    private ItemId? _currentRequestItemId;
    private bool _sellingHq;
    private bool _undercutHqOnly;
    private int _eventBatchCount;
    private readonly Dictionary<ulong, string> _eventRetainerNames = new();
    private readonly Dictionary<ulong, MarketListing> _eventListings = new();
    private DateTime _requestStartedAt;
    private DateTime _lastChangeAt;
    private int _lastSeenListingCount;
    private int _lastSeenBatchCount;
    private long _lastSeenEntryCount;
    private bool _adoptedExistingRequest;
    private string? _lastProxyTrace;

    private readonly record struct GameListing(ulong ListingId, MarketListing Listing, string Seller);

    public MarketBoardService(PricingStrategy pricingStrategy)
    {
        _pricingStrategy = pricingStrategy;
        Svc.MarketBoard.OfferingsReceived += OnMarketBoardOfferingsReceived;
    }

    /// <summary>
    /// Starts tracking a price request for <paramref name="itemId"/>. With <paramref name="adoptExistingRequest"/>,
    /// no new request is sent by the caller: the request already made for this sell window (e.g. by another
    /// plugin's auto Compare Prices) is used, so its listings count as fresh even though their batches arrived
    /// before this call.
    /// </summary>
    public void BeginRequest(ItemId itemId, bool sellingHq, bool undercutHqOnly, bool adoptExistingRequest = false)
    {
        lock (_lock)
        {
            _adoptedExistingRequest = adoptExistingRequest;
            _lastSeenEntryCount = -1;
            _currentRequestItemId = itemId;
            _sellingHq = sellingHq;
            _undercutHqOnly = undercutHqOnly;
            _eventBatchCount = 0;
            _eventRetainerNames.Clear();
            _eventListings.Clear();
            _requestStartedAt = DateTime.UtcNow;
            _lastChangeAt = DateTime.UtcNow;
            _lastSeenListingCount = -1;
            _lastSeenBatchCount = -1;
            _lastProxyTrace = null;
        }

        MarketDiagnosticsService.Log($"BeginRequest item {itemId} (HQ: {sellingHq}, HQ only: {undercutHqOnly}, " +
            $"adopt existing request: {adoptExistingRequest}). " +
            $"{MarketDiagnosticsService.DescribeMarketbuddy()}");
    }

    /// <summary>
    /// Returns the total listing count (ListingCount) of the game's current market board request when it is for
    /// <paramref name="itemId"/>, or null when the game's data is for another item (or unavailable).
    /// 0 means a request for this item has not received its first batch yet (or nobody is selling it).
    /// </summary>
    public static uint? GetCurrentRequestListingCount(ItemId itemId)
    {
        var proxy = InfoProxyItemSearch.Instance();
        if (proxy == null || proxy->SearchItemId % 1_000_000 != itemId.Value % 1_000_000)
            return null;
        return proxy->ListingCount;
    }

    private double ElapsedMs => (DateTime.UtcNow - _requestStartedAt).TotalMilliseconds;

    /// <summary>One-line snapshot of InfoProxyItemSearch for diagnostics.</summary>
    public static string DescribeProxyState(uint itemId)
    {
        var proxy = InfoProxyItemSearch.Instance();
        if (proxy == null)
            return "proxy=null";
        int nonEmpty = 0, matching = 0;
        uint minPrice = uint.MaxValue;
        for (var i = 0; i < proxy->Listings.Length; i++)
        {
            ref var l = ref proxy->Listings[i];
            if (l.ListingId == 0)
                continue;
            nonEmpty++;
            if (l.ItemId % 1_000_000 != itemId % 1_000_000)
                continue;
            matching++;
            minPrice = Math.Min(minPrice, l.UnitPrice);
        }
        return $"SearchItemId={proxy->SearchItemId}, ListingCount={proxy->ListingCount}, " +
               $"EntryCount={proxy->InfoProxyPageInterface.InfoProxyInterface.EntryCount}, WaitingForListings={proxy->WaitingForListings}, " +
               $"RequestId={proxy->CurrentRequestId}/{proxy->NextRequestId}, slots non-empty={nonEmpty} matching={matching}" +
               (matching > 0 ? $" min={minPrice}" : "");
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
                var countKnown = false;
                var gameListings = proxyMatches ? ReadGameListings(proxy, itemId, out countKnown) : [];

                var trace = $"{DescribeProxyState(itemId.Value)}, event batches={_eventBatchCount} listings={_eventListings.Count}";
                if (trace != _lastProxyTrace)
                {
                    _lastProxyTrace = trace;
                    MarketDiagnosticsService.Log($"+{elapsedMs:F0}ms item {itemId}: {trace}");
                }

                long entryCount = proxyMatches ? proxy->InfoProxyPageInterface.InfoProxyInterface.EntryCount : 0;
                if (gameListings.Count != _lastSeenListingCount || _eventBatchCount != _lastSeenBatchCount
                    || entryCount != _lastSeenEntryCount)
                {
                    _lastSeenListingCount = gameListings.Count;
                    _lastSeenBatchCount = _eventBatchCount;
                    _lastSeenEntryCount = entryCount;
                    _lastChangeAt = now;
                }

                // At least one batch must have arrived for this request so the game data is not left over
                // from a previous request, unless an existing request made for this sell window was adopted.
                // (InfoProxyItemSearch.WaitingForListings is not used: it stays true even after all listings
                // have been received.)
                var fresh = _eventBatchCount > 0 || _adoptedExistingRequest;
                var stable = (now - _lastChangeAt).TotalMilliseconds >= QuietPeriodMs;
                var gameDataReady = proxyMatches && IsGameDataReady(proxy, gameListings, countKnown, entryCount);

                if (!(proxyMatches && fresh && stable && gameDataReady))
                {
                    if (elapsedMs < timeoutMs)
                        return false;

                    Svc.Log.Error(
                        $"[MarketBoardService] TIMEOUT waiting for market board price for item {itemId} after {timeoutMs}ms " +
                        $"(proxy item: {(proxy != null ? proxy->SearchItemId : 0)}, ListingCount: {(proxy != null ? proxy->ListingCount : 0)}, " +
                        $"listings in game slots: {gameListings.Count}, event batches: {_eventBatchCount}, event listings: {_eventListings.Count})");

                    if (proxy == null || !fresh || (gameListings.Count == 0 && _eventListings.Count == 0))
                    {
                        _currentRequestItemId = null;
                        return null;
                    }

                    // Use whatever was received: the game slots merged with the event listings
                    sellingPrice = CalculateSellingPrice(proxy, itemId, MergeWithEventListings(gameListings), "game slots + event");
                    _currentRequestItemId = null;
                    return sellingPrice != null ? true : null;
                }

                List<GameListing> listings;
                string source;
                if (!countKnown && _eventListings.Count == 0 && !_adoptedExistingRequest)
                {
                    // The event says nobody is selling the item; anything in the slots would be left over
                    listings = [];
                    source = "event (no listings)";
                }
                else
                {
                    listings = gameListings;
                    source = countKnown ? "game data" : "game slots";
                }

                sellingPrice = CalculateSellingPrice(proxy, itemId, listings, source);
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

    /// <summary>
    /// Reads the listings of the item from the game data. When ListingCount is set, the first ListingCount
    /// slots are used (<paramref name="countKnown"/> = true). When it is 0 (e.g. reset by a second Compare
    /// Prices request while the slots still hold the first request's listings), every non-empty slot for the
    /// item is used instead.
    /// </summary>
    // Caller must hold _lock
    private List<GameListing> ReadGameListings(InfoProxyItemSearch* proxy, ItemId itemId, out bool countKnown)
    {
        countKnown = proxy->ListingCount > 0;
        var count = countKnown ? (int)Math.Min(proxy->ListingCount, (uint)proxy->Listings.Length) : proxy->Listings.Length;
        var listings = new List<GameListing>(countKnown ? count : 20);
        for (var i = 0; i < count; i++)
        {
            ref var l = ref proxy->Listings[i];
            if (l.ListingId == 0 || l.ItemId % 1_000_000 != itemId.Value % 1_000_000)
                continue;

            if (!_eventRetainerNames.TryGetValue(l.ListingId, out var seller))
                seller = $"retainer {l.RetainerId:X}, not in event";
            listings.Add(new GameListing(
                l.ListingId,
                new MarketListing((int)Math.Clamp(l.UnitPrice, 1u, (uint)int.MaxValue), l.IsHqItem),
                seller));
        }
        return listings;
    }

    // Caller must hold _lock
    private bool IsGameDataReady(InfoProxyItemSearch* proxy, List<GameListing> gameListings, bool countKnown, long entryCount)
    {
        if (countKnown)
        {
            // ListingCount is the total; wait until all of them have been received (EntryCount)
            var expected = Math.Min(proxy->ListingCount, (uint)proxy->Listings.Length);
            return entryCount >= expected;
        }

        // ListingCount is 0 below this point
        if (_adoptedExistingRequest)
        {
            // The adopted request's batches arrived before BeginRequest, so the event cannot confirm them
            return gameListings.Count > 0;
        }

        // Nobody is selling the item (per the event)
        if (_eventListings.Count == 0)
            return true;
        if (gameListings.Count == 0)
            return false;

        // Without ListingCount, require every listing received via the event to be in the game slots.
        // The event only ever receives a subset of what the game receives, so this confirms the slots
        // hold this request's data (not a previous request's) at least up to what the event saw.
        var ids = new HashSet<ulong>(gameListings.Select(l => l.ListingId));
        return _eventListings.Keys.All(ids.Contains);
    }

    // Caller must hold _lock
    private List<GameListing> MergeWithEventListings(List<GameListing> gameListings)
    {
        var merged = new List<GameListing>(gameListings);
        var ids = new HashSet<ulong>(gameListings.Select(l => l.ListingId));
        foreach (var (id, listing) in _eventListings)
        {
            if (ids.Add(id))
                merged.Add(new GameListing(id, listing, _eventRetainerNames.GetValueOrDefault(id, "?") + ", via event"));
        }
        return merged;
    }

    // Caller must hold _lock
    private Price? CalculateSellingPrice(InfoProxyItemSearch* proxy, ItemId itemId, List<GameListing> listings, string source)
    {
        var missingFromEvent = listings.Count(l => !_eventListings.ContainsKey(l.ListingId));
        Svc.Log.Debug(
            $"[MarketBoardService] Item {itemId}: {listings.Count} listings from {source} " +
            $"(ListingCount={proxy->ListingCount}, {_eventBatchCount} event batches, {_eventListings.Count} listings via event, " +
            $"{missingFromEvent} missing from event, {(DateTime.UtcNow - _requestStartedAt).TotalMilliseconds:F0}ms after request): " +
            string.Join(", ", listings.OrderBy(l => l.Listing.PricePerUnit)
                .Select(l => $"{l.Listing.PricePerUnit}{(l.Listing.IsHq ? "HQ" : "")}({l.Seller})")));

        if (listings.Count == 0)
        {
            LogProxyDiagnostics(proxy, itemId);
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
            $"overall lowest {listings.Min(l => l.Listing.PricePerUnit)} gil, listings: {listings.Count} ({source}) " +
            $"-> selling price {sellingPrice.Value} gil");

        return sellingPrice;
    }

    // Caller must hold _lock
    private static void LogProxyDiagnostics(InfoProxyItemSearch* proxy, ItemId itemId)
    {
        var raw = new List<string>();
        for (var i = 0; i < proxy->Listings.Length && raw.Count < 5; i++)
        {
            ref var l = ref proxy->Listings[i];
            if (l.ListingId == 0)
                continue;
            raw.Add($"[{i}] id={l.ListingId:X} item={l.ItemId} price={l.UnitPrice} qty={l.Quantity} hq={l.IsHqItem}");
        }

        Svc.Log.Warning(
            $"[MarketBoardService] Game data diagnostics for item {itemId}: SearchItemId={proxy->SearchItemId}, " +
            $"ListingCount={proxy->ListingCount}, EntryCount={proxy->InfoProxyPageInterface.InfoProxyInterface.EntryCount}, " +
            $"WaitingForListings={proxy->WaitingForListings}, RequestId={proxy->CurrentRequestId}/{proxy->NextRequestId}, " +
            $"non-empty slots: {(raw.Count == 0 ? "none" : string.Join("; ", raw))}");
    }

    private void OnMarketBoardOfferingsReceived(IMarketBoardCurrentOfferings currentOfferings)
    {
        try
        {
            lock (_lock)
            {
                if (_currentRequestItemId == null)
                {
                    MarketDiagnosticsService.Log($"Offerings event outside a request: requestId={currentOfferings.RequestId}, " +
                        $"{currentOfferings.ItemListings.Count} listings" +
                        (currentOfferings.ItemListings.Count > 0 ? $", item {currentOfferings.ItemListings[0].ItemId}" : ""));
                    return;
                }

                MarketDiagnosticsService.Log(
                    $"+{ElapsedMs:F0}ms offerings event #{_eventBatchCount + 1}: requestId={currentOfferings.RequestId}, " +
                    $"{currentOfferings.ItemListings.Count} listings" +
                    (currentOfferings.ItemListings.Count > 0
                        ? $", item {currentOfferings.ItemListings[0].ItemId}, price {currentOfferings.ItemListings.Min(l => l.PricePerUnit)}-{currentOfferings.ItemListings.Max(l => l.PricePerUnit)}"
                        : ""));

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
                    {
                        _eventRetainerNames[listing.ListingId] = listing.RetainerName;
                        _eventListings[listing.ListingId] = new MarketListing(
                            (int)Math.Clamp(listing.PricePerUnit, 1u, (uint)int.MaxValue), listing.IsHq);
                    }
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
