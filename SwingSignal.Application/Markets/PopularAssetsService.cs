using Microsoft.Extensions.Caching.Memory;
using SwingSignal.Application.Abstractions.Persistence;
using SwingSignal.Application.Odds;
using SwingSignal.Contracts.Assets;

namespace SwingSignal.Application.Markets;

public class PopularAssetsService : IPopularAssetsService
{
    // Curated for now; switch to real search analytics once we track them
    private static readonly string[] PopularSymbols =
        ["BTCUSDT", "ETHUSDT", "SPY", "QQQ", "GC=F", "EURUSD=X"];

    private const int WindowDays = 90;   // sparkline lookback (trading days ~63, we take candles)
    private const int SparkPoints = 30;  // downsampled points per sparkline

    // Computing odds for six assets isn't free, and every landing/dashboard
    // visit asks for this — cache it here so every caller benefits.
    private const string CacheKey = "popular-assets";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly IAssetRepository _assets;
    private readonly ICandleRepository _candles;
    private readonly IHistoricalOddsService _odds;
    private readonly IMemoryCache _cache;

    public PopularAssetsService(
        IAssetRepository assets,
        ICandleRepository candles,
        IHistoricalOddsService odds,
        IMemoryCache cache)
    {
        _assets = assets;
        _candles = candles;
        _odds = odds;
        _cache = cache;
    }

    public async Task<List<PopularAssetDto>> GetPopularAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out List<PopularAssetDto>? cached) && cached is not null)
            return cached;

        var results = await BuildAsync(ct);
        _cache.Set(CacheKey, results, CacheTtl);
        return results;
    }

    private async Task<List<PopularAssetDto>> BuildAsync(CancellationToken ct)
    {
        var results = new List<PopularAssetDto>();

        foreach (var symbol in PopularSymbols)
        {
            var asset = await _assets.GetBySymbolAsync(symbol, ct);
            if (asset is null) continue;

            var candles = await _candles.GetDailyHistoryAsync(asset.Id, ct);
            if (candles.Count < 10) continue;

            var window = candles
                .Where(c => c.OpenTime >= DateTime.UtcNow.AddDays(-WindowDays))
                .Select(c => c.Close)
                .ToList();

            if (window.Count < 5)
                window = candles.TakeLast(Math.Min(candles.Count, 60)).Select(c => c.Close).ToList();

            var current = window[^1];
            var changePct = window[0] != 0
                ? Math.Round((current - window[0]) / window[0] * 100, 1)
                : 0;

            double? odds3M = null, baseRate3M = null, edge3M = null;
            try
            {
                var odds = await _odds.GetOddsForDaysAsync(symbol, 90, ct: ct);
                if (odds.Odds.TotalCases > 0)
                {
                    odds3M = odds.Odds.PositiveOdds;
                    baseRate3M = odds.Odds.BaseRate;
                    edge3M = odds.Odds.Edge;
                }
            }
            catch
            {
                // odds unavailable (young asset / no macro data) — card still renders
            }

            results.Add(new PopularAssetDto(
                Symbol: asset.Symbol,
                Name: asset.Name,
                CurrentPrice: current,
                ChangePct: changePct,
                Spark: Downsample(window, SparkPoints),
                Odds3M: odds3M,
                BaseRate3M: baseRate3M,
                Edge3M: edge3M));
        }

        return results;
    }

    // Evenly sampled points, always keeping the most recent close as the last point
    private static List<decimal> Downsample(List<decimal> series, int points)
    {
        if (series.Count <= points) return series;

        var result = new List<decimal>(points);
        for (var i = 0; i < points - 1; i++)
        {
            var idx = (int)((long)i * (series.Count - 1) / (points - 1));
            result.Add(series[idx]);
        }
        result.Add(series[^1]);
        return result;
    }
}
