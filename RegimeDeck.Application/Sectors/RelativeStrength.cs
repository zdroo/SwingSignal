using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Application.Sectors;

/// Relative strength = how much an asset out- or under-performed a benchmark
/// over a lookback window, in percentage points. Positive means the asset
/// beat the benchmark ("money rotating in"). Pure so it's unit-testable.
public static class RelativeStrength
{
    /// Both series must be ascending by open time (as GetDailyHistoryAsync
    /// returns them). Null when either lacks enough history to span the window.
    public static double? Compute(
        IReadOnlyList<Candle> asset, IReadOnlyList<Candle> benchmark, int lookbackDays)
    {
        var assetReturn = ReturnOver(asset, lookbackDays);
        var benchmarkReturn = ReturnOver(benchmark, lookbackDays);
        if (assetReturn is null || benchmarkReturn is null) return null;

        return Math.Round(assetReturn.Value - benchmarkReturn.Value, 1);
    }

    // Percent change from the first candle at/after (last - lookback) to the last.
    private static double? ReturnOver(IReadOnlyList<Candle> candles, int lookbackDays)
    {
        if (candles.Count < 2) return null;

        var last = candles[^1];
        var cutoff = last.OpenTime.AddDays(-lookbackDays);

        Candle? start = null;
        foreach (var candle in candles)
        {
            if (candle.OpenTime >= cutoff) { start = candle; break; }
        }

        if (start is null || start.Close == 0 || start.OpenTime >= last.OpenTime) return null;

        return (double)((last.Close - start.Close) / start.Close) * 100;
    }
}
