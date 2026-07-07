using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SwingSignal.Application.Abstractions.Ingestion;
using SwingSignal.Application.Common;
using SwingSignal.Domain.Entities;
using SwingSignal.Domain.Enums;
using SwingSignal.Infrastructure.ExternalClients;
using SwingSignal.Infrastructure.Persistence;

namespace SwingSignal.Infrastructure.Ingestion;

public class OnDemandIngestionService : IAssetIngestionService
{
    private readonly SwingSignalDbContext _db;
    private readonly BinanceApiClient _binance;
    private readonly YahooFinanceApiClient _yahoo;
    private readonly ILogger<OnDemandIngestionService> _logger;

    public OnDemandIngestionService(
        SwingSignalDbContext db,
        BinanceApiClient binance,
        YahooFinanceApiClient yahoo,
        ILogger<OnDemandIngestionService> logger)
    {
        _db = db;
        _binance = binance;
        _yahoo = yahoo;
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

            // Has the asset but no candles yet — ingest now
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
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Registered new asset on-demand: {Symbol} ({MarketType})", symbol, marketType);

        await IngestCandlesAsync(asset, ct);
        return asset;
    }

    private async Task IngestCandlesAsync(Asset asset, CancellationToken ct)
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
                return;
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
        }
    }

    private static string BuildDisplayName(string symbol, MarketType marketType) => marketType switch
    {
        MarketType.Forex     => symbol.Replace("=X", "").Insert(3, "/"),
        MarketType.Commodity => symbol.Replace("=F", "") + " Futures",
        MarketType.Crypto    => symbol.Replace("USDT", "/USDT"),
        _                    => symbol
    };
}
