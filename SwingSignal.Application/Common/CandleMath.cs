using SwingSignal.Domain.Entities;

namespace SwingSignal.Application.Common;

/// Shared candle-series math used by the odds, backtest and asset-state logic.
/// One implementation so live predictions and backtests can never diverge.
public static class CandleMath
{
    /// Candles per month used when sampling rolling windows (~21 trading days).
    public const int MonthlyStride = 21;

    /// Minimum sampled windows before a base rate is considered meaningful.
    public const int MinBaseRateSamples = 24;

    /// Binary search for the candle index nearest to a target date, within a window.
    /// Returns -1 when no candle falls inside the window.
    public static int FindNearestIndex(List<Candle> candles, DateTime target, int windowDays)
    {
        if (candles.Count == 0) return -1;

        int lo = 0, hi = candles.Count - 1;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (candles[mid].OpenTime < target) lo = mid + 1; else hi = mid;
        }

        var best = -1;
        var bestGap = double.MaxValue;
        for (var i = Math.Max(0, lo - 1); i <= Math.Min(candles.Count - 1, lo + 1); i++)
        {
            var gap = Math.Abs((candles[i].OpenTime - target).TotalDays);
            if (gap < bestGap) { bestGap = gap; best = i; }
        }

        return bestGap <= windowDays ? best : -1;
    }

    public static Candle? FindNearest(List<Candle> candles, DateTime target, int windowDays)
    {
        var idx = FindNearestIndex(candles, target, windowDays);
        return idx < 0 ? null : candles[idx];
    }

    /// Exit-candle matching window for a horizon: tight for short horizons so a
    /// 7-day prediction can't match an exit candle sitting next to the entry.
    public static int ExitWindow(int horizonDays) => Math.Clamp(horizonDays / 3, 2, 7);

    /// Approximate trading-day count for a calendar-day horizon.
    public static int HorizonCandles(int horizonDays) => Math.Max(1, (int)(horizonDays * 5.0 / 7.0));

    public static decimal PercentReturn(decimal entry, decimal exit) =>
        Math.Round((exit - entry) / entry * 100, 2);

    /// Monthly-strided rolling windows over the full history, ordered by exit
    /// date so walk-forward consumers can advance a pointer.
    public static List<(DateTime ExitDate, bool Positive)> SampleOutcomes(List<Candle> candles, int horizonDays)
    {
        var horizon = HorizonCandles(horizonDays);
        var samples = new List<(DateTime, bool)>();

        for (var i = 0; i + horizon < candles.Count; i += MonthlyStride)
        {
            var exit = candles[i + horizon];
            samples.Add((exit.OpenTime, exit.Close > candles[i].Close));
        }

        return samples.OrderBy(s => s.Item1).ToList();
    }

    /// Share of historical windows of this length that ended positive —
    /// across all history, or only windows exiting on/after `since` when a
    /// trailing cutoff is given. Null when there isn't enough history for a
    /// meaningful base rate.
    public static double? ComputeBaseRate(List<Candle> candles, int horizonDays, DateTime? since = null)
    {
        var samples = SampleOutcomes(candles, horizonDays);
        if (since is not null)
            samples = samples.Where(s => s.ExitDate >= since).ToList();

        if (samples.Count < MinBaseRateSamples) return null;

        return (double)samples.Count(s => s.Positive) / samples.Count * 100;
    }
}
