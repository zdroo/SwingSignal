using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SwingSignal.Domain.Entities;
using SwingSignal.Domain.Enums;
using SwingSignal.Infrastructure.ExternalClients;
using SwingSignal.Infrastructure.Persistence;

namespace SwingSignal.Infrastructure.Ingestion;

/// One-time splice of pre-Binance daily history from CoinMetrics. Binance
/// stays authoritative from each pair's listing forward; this only inserts
/// strictly older candles, subject to two floors:
///  - a global floor (default 2014-01-01): pre-2013 crypto price discovery
///    was too thin to treat as analog evidence;
///  - a per-asset maturity floor (first traded date + 2 years): an asset's
///    infancy hyper-growth (ETH going $1 -> $300) poisons odds and price
///    targets — validated July 2026, where ETH's 2015-17 backfill degraded
///    2022+ Brier while BTC's mature 2014+ history helped at 180d.
public class CryptoHistoryBackfillService
{
    private static readonly DateTime DefaultFloor = new(2014, 1, 1);
    private static readonly DateTime GenesisProbe = new(2009, 1, 1); // before any crypto traded
    private const int MaturityYears = 2;

    private readonly SwingSignalDbContext _db;
    private readonly CoinMetricsApiClient _coinMetrics;
    private readonly ILogger<CryptoHistoryBackfillService> _logger;
    private readonly DateTime _floor;

    public CryptoHistoryBackfillService(
        SwingSignalDbContext db,
        CoinMetricsApiClient coinMetrics,
        ILogger<CryptoHistoryBackfillService> logger,
        IConfiguration config)
    {
        _db = db;
        _coinMetrics = coinMetrics;
        _logger = logger;
        _floor = config.GetValue<DateTime?>("CryptoHistory:BackfillFloor") ?? DefaultFloor;
    }

    public async Task BackfillAsync(Asset asset, CancellationToken ct = default)
    {
        if (asset.MarketType != MarketType.Crypto) return;

        var earliest = await _db.Candles
            .Where(c => c.AssetId == asset.Id && c.Interval == CandleInterval.OneDay)
            .MinAsync(c => (DateTime?)c.OpenTime, ct);

        // Binance ingestion must run first; and if history already reaches the
        // floor (previous backfill, or an early listing), there is nothing to add.
        if (earliest is null || earliest <= _floor.AddDays(7)) return;

        var coin = CoinOf(asset.Symbol);
        var raw = await _coinMetrics.GetDailyClosesAsync(coin, GenesisProbe, earliest.Value, ct);
        if (raw.Count == 0) return;

        // The asset's first two years of existence are excluded regardless of
        // the global floor — infancy returns are not analog evidence.
        var maturityFloor = raw[0].OpenTime.AddYears(MaturityYears);
        var floor = maturityFloor > _floor ? maturityFloor : _floor;

        var toInsert = raw
            .Where(r => r.OpenTime >= floor && r.OpenTime < earliest && r.Close > 0)
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

        if (toInsert.Count == 0)
        {
            _logger.LogInformation("No pre-{Earliest:yyyy-MM-dd} history usable for {Symbol} (effective floor {Floor:yyyy-MM-dd})",
                earliest, asset.Symbol, floor);
            return;
        }

        await _db.Candles.AddRangeAsync(toInsert, ct);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Backfilled {Count} daily candles for {Symbol}: {From:yyyy-MM-dd} -> {To:yyyy-MM-dd} (Binance takes over at {Earliest:yyyy-MM-dd})",
            toInsert.Count, asset.Symbol, toInsert[0].OpenTime, toInsert[^1].OpenTime, earliest);
    }

    private static string CoinOf(string symbol) =>
        symbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase) ||
        symbol.EndsWith("BUSD", StringComparison.OrdinalIgnoreCase)
            ? symbol[..^4]
            : symbol;
}
