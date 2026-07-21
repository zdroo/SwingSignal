using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RegimeDeck.Application.Abstractions.Ingestion;
using RegimeDeck.Application.Common;
using RegimeDeck.Domain.Entities;
using RegimeDeck.Domain.Enums;
using RegimeDeck.Infrastructure.ExternalClients;
using RegimeDeck.Infrastructure.Persistence;

namespace RegimeDeck.Infrastructure.Ingestion;

public class OnDemandIngestionService : IAssetIngestionService
{
    private readonly RegimeDeckDbContext _db;
    private readonly BinanceApiClient _binance;
    private readonly YahooFinanceApiClient _yahoo;
    private readonly CryptoHistoryBackfillService _backfill;
    private readonly ILogger<OnDemandIngestionService> _logger;

    public OnDemandIngestionService(
        RegimeDeckDbContext db,
        BinanceApiClient binance,
        YahooFinanceApiClient yahoo,
        CryptoHistoryBackfillService backfill,
        ILogger<OnDemandIngestionService> logger)
    {
        _db = db;
        _binance = binance;
        _yahoo = yahoo;
        _backfill = backfill;
        _logger = logger;
    }

    // Returns the asset (existing or newly ingested). Returns null if fetching failed.
    public async Task<Asset?> EnsureIngestedAsync(string symbol, CancellationToken ct = default)
    {
        var existing = await _db.Assets
            .FirstOrDefaultAsync(a => a.Symbol == symbol, ct);

        if (existing is not null)
        {
            // Asset already registered — make sure it has at least some daily candles
            var hasCandles = await _db.Candles
                .AnyAsync(c => c.AssetId == existing.Id && c.Interval == CandleInterval.OneDay, ct);

            if (hasCandles) return existing;

            // Has the asset but no candles yet — ingest now. An already-registered
            // asset gets the benefit of the doubt (it may be a seeded symbol whose
            // scheduled ingestion just hasn't run, or a transient provider outage),
            // so we return it either way rather than deleting it.
            await IngestCandlesAsync(existing, ct);
            return existing;
        }

        // Completely new symbol — register and ingest
        var marketType = SymbolNormalizer.DetectMarketType(symbol);
        var name = BuildDisplayName(symbol, marketType);

        var asset = new Asset
        {
            Symbol     = symbol,
            Name       = name,
            MarketType = marketType,
            IsActive   = true
        };

        _db.Assets.Add(asset);
        await _db.SaveChangesAsync(ct); // need the Id for the candle FK
        _logger.LogInformation("Registered new asset on-demand: {Symbol} ({MarketType})", symbol, marketType);

        // A brand-new symbol with no fetchable price data is a typo/unsupported
        // ticker — don't leave a junk asset row behind (which would also make
        // the odds endpoint return an empty 200 instead of "not supported").
        if (!await IngestCandlesAsync(asset, ct))
        {
            _db.Assets.Remove(asset);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Unsupported symbol had no data, removed: {Symbol}", symbol);
            return null;
        }

        return asset;
    }

    // True when price data is available for the asset (candles were fetched or
    // already present); false when the provider returned nothing or errored.
    private async Task<bool> IngestCandlesAsync(Asset asset, CancellationToken ct)
    {
        try
        {
            List<RawCandle> raw;

            if (asset.MarketType == MarketType.Crypto)
                raw = await _binance.GetCandlesAsync(asset.Symbol, CandleInterval.OneDay, DateTime.UtcNow.AddYears(-10), ct: ct);
            else
                // Yahoo: take full available history (30y) for deeper backtests
                raw = await _yahoo.GetCandlesAsync(asset.Symbol, CandleInterval.OneDay, DateTime.UtcNow.AddYears(-30), ct);

            if (raw.Count == 0)
            {
                _logger.LogWarning("No candles returned for {Symbol}", asset.Symbol);
                return false;
            }

            var existingTimes = (await _db.Candles
                .Where(c => c.AssetId == asset.Id && c.Interval == CandleInterval.OneDay)
                .Select(c => c.OpenTime)
                .ToListAsync(ct))
                .ToHashSet();

            var newCandles = raw
                .Where(r => !existingTimes.Contains(r.OpenTime))
                .Select(r => new Candle
                {
                    AssetId  = asset.Id,
                    OpenTime = r.OpenTime,
                    Open     = r.Open,
                    High     = r.High,
                    Low      = r.Low,
                    Close    = r.Close,
                    Volume   = r.Volume,
                    Interval = CandleInterval.OneDay
                })
                .ToList();

            if (newCandles.Count > 0)
            {
                await _db.Candles.AddRangeAsync(newCandles, ct);
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Ingested {Count} candles on-demand for {Symbol}", newCandles.Count, asset.Symbol);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "On-demand ingestion failed for {Symbol}", asset.Symbol);
            return false;
        }

        // The asset now has its primary candles. The crypto pre-Binance backfill
        // is a non-fatal enhancement (deeper history) — isolate it so its failure
        // can never discard an asset that already has valid data.
        try
        {
            await _backfill.BackfillAsync(asset, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Backfill failed for {Symbol} (non-fatal)", asset.Symbol);
        }

        return true;
    }

    private static string BuildDisplayName(string symbol, MarketType marketType) => marketType switch
    {
        MarketType.Forex     => symbol.Replace("=X", "").Insert(3, "/"),
        MarketType.Commodity => symbol.Replace("=F", "") + " Futures",
        MarketType.Crypto    => symbol.Replace("USDT", "/USDT"),
        _                    => symbol
    };
}
