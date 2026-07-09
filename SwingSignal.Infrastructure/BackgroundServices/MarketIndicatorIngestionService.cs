using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SwingSignal.Domain.Entities;
using SwingSignal.Domain.Enums;
using SwingSignal.Infrastructure.ExternalClients;
using SwingSignal.Infrastructure.Persistence;

namespace SwingSignal.Infrastructure.BackgroundServices;

// Ingests market-derived macro indicators (VIX, DXY, Copper, Gold) from Yahoo Finance
// and the Crypto Fear & Greed index. These are stored as MacroDataPoints (daily close),
// not as tradable assets.
public class MarketIndicatorIngestionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MarketIndicatorIngestionService> _logger;

    private static readonly Dictionary<MacroIndicatorType, string> YahooSymbols = new()
    {
        [MacroIndicatorType.VIX]         = "^VIX",
        [MacroIndicatorType.DollarIndex] = "DX-Y.NYB",
        [MacroIndicatorType.Copper]      = "HG=F",
        [MacroIndicatorType.GoldPrice]   = "GC=F",
    };

    public MarketIndicatorIngestionService(
        IServiceScopeFactory scopeFactory,
        ILogger<MarketIndicatorIngestionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var ct = stoppingToken;
        while (!ct.IsCancellationRequested)
        {
            await IngestAllAsync(ct);
            await Task.Delay(TimeSpan.FromHours(12), ct);
        }
    }

    private async Task IngestAllAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SwingSignalDbContext>();
        var yahoo = scope.ServiceProvider.GetRequiredService<YahooFinanceApiClient>();
        var fearGreed = scope.ServiceProvider.GetRequiredService<FearGreedApiClient>();

        foreach (var (indicatorType, symbol) in YahooSymbols)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var latestDate = await db.MacroDataPoints
                    .Where(m => m.IndicatorType == indicatorType)
                    .MaxAsync(m => (DateTime?)m.Date, ct);

                var from = latestDate?.AddDays(1) ?? DateTime.UtcNow.AddYears(-30);
                var candles = await yahoo.GetCandlesAsync(symbol, CandleInterval.OneDay, from, ct);

                if (candles.Count == 0) continue;

                await SavePointsAsync(
                    db, indicatorType,
                    candles.Select(c => (c.OpenTime.Date, c.Close)),
                    "YahooFinance", ct);

                // Be polite to Yahoo between symbols
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ingest market indicator {Indicator}", indicatorType);
            }
        }

        try
        {
            var history = await fearGreed.GetHistoryAsync(ct);
            if (history.Count > 0)
            {
                await SavePointsAsync(
                    db, MacroIndicatorType.CryptoFearGreed,
                    history.Select(h => (h.Date, h.Value)),
                    "Alternative.me", ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ingest Fear & Greed index");
        }

        await IngestCryptoNativeAsync(scope, db, ct);
    }

    // Crypto-native cycle gauges for the crypto matching profile. Each source
    // is independent — one failing must not stop the others.
    private async Task IngestCryptoNativeAsync(IServiceScope scope, SwingSignalDbContext db, CancellationToken ct)
    {
        // History floor: matches the earliest usable crypto candle era; there
        // is no point storing gauge values no analog can pair with.
        var floor = new DateTime(2013, 1, 1);

        try
        {
            // Full range every run (chunked upstream); SavePointsAsync dedups.
            // Incremental-from-latest would never heal gaps behind the newest point.
            var bitcoinData = scope.ServiceProvider.GetRequiredService<BitcoinDataApiClient>();
            var mvrv = await bitcoinData.GetMvrvAsync(floor, DateTime.UtcNow.Date, ct);
            await SavePointsAsync(db, MacroIndicatorType.CryptoMvrv, mvrv, "bitcoin-data.com", ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ingest MVRV");
        }

        try
        {
            var blockchainInfo = scope.ServiceProvider.GetRequiredService<BlockchainInfoApiClient>();

            // Full revenue series every run: the Puell denominator (365d mean)
            // needs the whole history anyway; SavePointsAsync dedups inserts.
            var revenue = await blockchainInfo.GetChartAsync("miners-revenue", ct);
            await SavePointsAsync(db, MacroIndicatorType.CryptoMinerPuell,
                ToPuell(revenue).Where(p => p.Date >= floor), "blockchain.info", ct);

            var hashRate = await blockchainInfo.GetChartAsync("hash-rate", ct);
            await SavePointsAsync(db, MacroIndicatorType.CryptoHashRate,
                hashRate.Where(p => p.Date >= floor), "blockchain.info", ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ingest miner metrics");
        }

        try
        {
            var coinMetrics = scope.ServiceProvider.GetRequiredService<CoinMetricsApiClient>();
            var latest = await LatestDateAsync(db, MacroIndicatorType.StablecoinSupply, ct);
            var from = latest?.AddDays(1) ?? floor;

            var usdt = await coinMetrics.GetMetricSeriesAsync("usdt", "CapMrktCurUSD", from, DateTime.UtcNow.Date, ct);
            var usdc = await coinMetrics.GetMetricSeriesAsync("usdc", "CapMrktCurUSD", from, DateTime.UtcNow.Date, ct);

            var usdcByDate = usdc.ToDictionary(p => p.Date.Date, p => p.Value);
            var combined = usdt.Select(p =>
                (p.Date.Date, p.Value + usdcByDate.GetValueOrDefault(p.Date.Date)));

            await SavePointsAsync(db, MacroIndicatorType.StablecoinSupply, combined, "CoinMetrics", ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ingest stablecoin supply");
        }

        try
        {
            await IngestEthBtcRatioAsync(db, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compute ETH/BTC ratio");
        }

        try
        {
            await IngestMayerMultipleAsync(db, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compute Mayer Multiple");
        }
    }

    // BTC close / its 200-day average — the price-based MVRV stand-in,
    // computed from our own candles so it reaches as deep as BTC history does
    private async Task IngestMayerMultipleAsync(SwingSignalDbContext db, CancellationToken ct)
    {
        var closes = await db.Candles
            .Where(c => c.Interval == CandleInterval.OneDay && c.Asset.Symbol == "BTCUSDT")
            .OrderBy(c => c.OpenTime)
            .Select(c => new { c.OpenTime, c.Close })
            .ToListAsync(ct);

        const int window = 200;
        var points = new List<(DateTime Date, decimal Value)>();
        decimal rollingSum = 0;

        for (var i = 0; i < closes.Count; i++)
        {
            rollingSum += closes[i].Close;
            if (i >= window) rollingSum -= closes[i - window].Close;
            if (i >= window - 1 && rollingSum > 0)
                points.Add((closes[i].OpenTime.Date, closes[i].Close / (rollingSum / window)));
        }

        await SavePointsAsync(db, MacroIndicatorType.CryptoMayerMultiple, points, "Computed", ct);
    }

    /// Puell multiple: each day's miner revenue over the mean of the trailing
    /// 365 observations (inclusive). Days without a full window are skipped.
    private static IEnumerable<(DateTime Date, decimal Value)> ToPuell(
        List<(DateTime Date, decimal Value)> revenue)
    {
        const int window = 365;
        decimal rollingSum = 0;

        for (var i = 0; i < revenue.Count; i++)
        {
            rollingSum += revenue[i].Value;
            if (i >= window) rollingSum -= revenue[i - window].Value;
            if (i >= window - 1)
                yield return (revenue[i].Date, revenue[i].Value / (rollingSum / window));
        }
    }

    // ETH/BTC from our own candles — no external source needed
    private async Task IngestEthBtcRatioAsync(SwingSignalDbContext db, CancellationToken ct)
    {
        var closes = await db.Candles
            .Where(c => c.Interval == CandleInterval.OneDay &&
                        (c.Asset.Symbol == "BTCUSDT" || c.Asset.Symbol == "ETHUSDT"))
            .Select(c => new { c.Asset.Symbol, c.OpenTime, c.Close })
            .ToListAsync(ct);

        var btc = closes.Where(c => c.Symbol == "BTCUSDT")
            .ToDictionary(c => c.OpenTime.Date, c => c.Close);

        var ratio = closes
            .Where(c => c.Symbol == "ETHUSDT" && btc.ContainsKey(c.OpenTime.Date) && btc[c.OpenTime.Date] > 0)
            .Select(c => (c.OpenTime.Date, c.Close / btc[c.OpenTime.Date]));

        await SavePointsAsync(db, MacroIndicatorType.CryptoEthBtcRatio, ratio, "Computed", ct);
    }

    private static Task<DateTime?> LatestDateAsync(
        SwingSignalDbContext db, MacroIndicatorType type, CancellationToken ct) =>
        db.MacroDataPoints
            .Where(m => m.IndicatorType == type)
            .MaxAsync(m => (DateTime?)m.Date, ct);

    private async Task SavePointsAsync(
        SwingSignalDbContext db,
        MacroIndicatorType indicatorType,
        IEnumerable<(DateTime Date, decimal Value)> points,
        string source,
        CancellationToken ct)
    {
        var existingDates = (await db.MacroDataPoints
            .Where(m => m.IndicatorType == indicatorType)
            .Select(m => m.Date)
            .ToListAsync(ct))
            .ToHashSet();

        var newPoints = points
            .GroupBy(p => p.Date)
            .Select(g => g.Last())
            .Where(p => !existingDates.Contains(p.Date))
            .Select(p => new MacroDataPoint
            {
                IndicatorType = indicatorType,
                Date = p.Date,
                Value = p.Value,
                Source = source
            })
            .ToList();

        if (newPoints.Count > 0)
        {
            await db.MacroDataPoints.AddRangeAsync(newPoints, ct);
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Ingested {Count} points for {Indicator}", newPoints.Count, indicatorType);
        }
    }
}
