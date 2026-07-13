using RegimeDeck.Application.Abstractions.Ingestion;
using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Application.Common;
using RegimeDeck.Application.Odds;
using RegimeDeck.Contracts.Watchlist;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Application.Watchlist;

public class WatchlistService : IWatchlistService
{
    /// Bounds the overview computation — each row re-runs the odds engine
    public const int MaxItems = 15;

    private readonly IWatchlistRepository _watchlist;
    private readonly IAssetIngestionService _ingestion;
    private readonly IHistoricalOddsService _odds;

    public WatchlistService(
        IWatchlistRepository watchlist,
        IAssetIngestionService ingestion,
        IHistoricalOddsService odds)
    {
        _watchlist = watchlist;
        _ingestion = ingestion;
        _odds = odds;
    }

    public async Task<List<WatchlistItemDto>> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var items = await _watchlist.GetByUserAsync(userId, ct);
        return items.Select(ToDto).ToList();
    }

    public async Task<WatchlistItemDto> AddAsync(Guid userId, string symbol, CancellationToken ct = default)
    {
        var normalized = SymbolNormalizer.Normalize(symbol);
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ValidationException("Symbol is required.");

        if (await _watchlist.GetAsync(userId, normalized, ct) is not null)
            throw new ConflictException($"{normalized} is already on your watchlist.");

        if (await _watchlist.CountByUserAsync(userId, ct) >= MaxItems)
            throw new ValidationException($"Watchlist is limited to {MaxItems} assets.");

        var asset = await _ingestion.EnsureIngestedAsync(normalized, ct)
            ?? throw new ValidationException($"Symbol '{normalized}' is not supported.");

        var item = new WatchlistItem
        {
            UserId = userId,
            Symbol = asset.Symbol,
            Name = asset.Name,
            CreatedAt = DateTime.UtcNow,
        };
        await _watchlist.AddAsync(item, ct);

        return ToDto(item);
    }

    public async Task RemoveAsync(Guid userId, string symbol, CancellationToken ct = default)
    {
        var normalized = SymbolNormalizer.Normalize(symbol);
        var item = await _watchlist.GetAsync(userId, normalized, ct)
            ?? throw new NotFoundException($"{normalized} is not on your watchlist.");

        await _watchlist.DeleteAsync(item, ct);
    }

    public async Task<List<WatchlistRowDto>> GetOverviewAsync(Guid userId, CancellationToken ct = default)
    {
        var items = await _watchlist.GetByUserAsync(userId, ct);
        var rows = new List<WatchlistRowDto>(items.Count);

        foreach (var item in items)
        {
            rows.Add(await BuildRowAsync(item, ct));
        }

        return rows;
    }

    // One asset failing (delisted, ingestion hiccup) must not take down the
    // whole overview — its row degrades to name-only.
    private async Task<WatchlistRowDto> BuildRowAsync(WatchlistItem item, CancellationToken ct)
    {
        try
        {
            var odds = await _odds.GetOddsAsync(item.Symbol, ct);
            var summary = ThreeMonthSummary.From(odds);

            return new WatchlistRowDto(
                item.Symbol,
                item.Name,
                odds.CurrentPrice,
                summary.Odds3M,
                summary.BaseRate3M,
                summary.Edge3M,
                odds.TradeRead,
                item.CreatedAt);
        }
        catch (AppException)
        {
            return new WatchlistRowDto(
                item.Symbol, item.Name, null, null, null, null, null, item.CreatedAt);
        }
    }

    private static WatchlistItemDto ToDto(WatchlistItem item) =>
        new(item.Symbol, item.Name, item.CreatedAt);
}
