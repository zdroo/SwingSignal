using SwingSignal.Application.Abstractions.Persistence;
using SwingSignal.Application.Common;
using SwingSignal.Application.Regime;
using SwingSignal.Contracts.Regime;
using SwingSignal.Domain.Entities;

namespace SwingSignal.Application.Odds;

public class HistoricalOddsService : IHistoricalOddsService
{
    private const string Disclaimer =
        "Historical data only. Past macro environments do not guarantee future performance. Not financial advice.";

    private readonly IAssetRepository _assets;
    private readonly ICandleRepository _candles;
    private readonly IMacroRegimeService _regime;
    private readonly IAssetExplainerService _explainer;

    public HistoricalOddsService(
        IAssetRepository assets,
        ICandleRepository candles,
        IMacroRegimeService regime,
        IAssetExplainerService explainer)
    {
        _assets = assets;
        _candles = candles;
        _regime = regime;
        _explainer = explainer;
    }

    public async Task<AssetOddsDto> GetOddsAsync(string symbol, int topK = 10, CancellationToken ct = default)
    {
        var asset = await _assets.GetBySymbolAsync(symbol.ToUpper(), ct)
            ?? throw new KeyNotFoundException($"Asset {symbol.ToUpper()} not found");

        // Kernel weighting uses a wide set of declustered analogs, not just the top handful
        var matches = await _regime.FindSimilarPeriodsAsync(MatchingOptions.AnalogCount, ct);
        var candles = await _candles.GetDailyHistoryAsync(asset.Id, ct);

        if (matches.Count == 0 || candles.Count == 0)
            return EmptyOdds(asset);

        var currentPrice = candles[^1].Close;
        var weighted = ConditionOnAssetState(candles, ApplyKernelWeights(matches));

        var explanations = await _explainer.GenerateAsync(asset.Symbol, asset.MarketType, matches, candles, ct);

        return new AssetOddsDto(
            Symbol:      asset.Symbol,
            Name:        asset.Name,
            MatchesUsed: matches.Count,
            CurrentPrice: currentPrice,
            OneMonth:    ComputeOdds(ComputeReturns(candles, weighted, 30), currentPrice, CandleMath.ComputeBaseRate(candles, 30)),
            ThreeMonths: ComputeOdds(ComputeReturns(candles, weighted, 90), currentPrice, CandleMath.ComputeBaseRate(candles, 90)),
            SixMonths:   ComputeOdds(ComputeReturns(candles, weighted, 180), currentPrice, CandleMath.ComputeBaseRate(candles, 180)),
            Explanations: explanations,
            Disclaimer:  Disclaimer,
            Breakdown:   ComputeBreakdown(candles, matches)
        );
    }

    // Splits the analogs by the asset's own price state (above/below its
    // 200-day average) and reports each group's 3M outcomes. Descriptive
    // context only: this split looked predictive in-sample but failed our
    // walk-forward validation, so it does NOT influence the headline odds.
    private static AnalogBreakdownDto ComputeBreakdown(
        List<Candle> candles, List<HistoricalMatchDto> matches)
    {
        const int horizonDays = 90;
        var exitWindow = CandleMath.ExitWindow(horizonDays);

        var points = new List<AnalogPointDto>();
        var aboveReturns = new List<decimal>();
        var belowReturns = new List<decimal>();

        foreach (var match in matches)
        {
            var idx = CandleMath.FindNearestIndex(candles, match.Date, 7);
            if (idx < 0) continue;

            var above = AboveMa200At(candles, idx);
            points.Add(new AnalogPointDto(match.Date, above));

            if (above is null) continue;

            var exit = CandleMath.FindNearest(candles, match.Date.AddDays(horizonDays), exitWindow);
            if (exit is null || exit.OpenTime <= candles[idx].OpenTime) continue;

            var ret = CandleMath.PercentReturn(candles[idx].Close, exit.Close);
            (above.Value ? aboveReturns : belowReturns).Add(ret);
        }

        return new AnalogBreakdownDto(
            CurrentAboveMa200: AboveMa200At(candles, candles.Count - 1),
            AboveCount: aboveReturns.Count,
            AboveOdds3M: GroupOdds(aboveReturns),
            AboveMedian3M: GroupMedian(aboveReturns),
            BelowCount: belowReturns.Count,
            BelowOdds3M: GroupOdds(belowReturns),
            BelowMedian3M: GroupMedian(belowReturns),
            Points: points);
    }

    private static bool? AboveMa200At(List<Candle> candles, int index)
    {
        if (index < 200) return null;

        decimal sum = 0;
        for (var i = index - 199; i <= index; i++)
            sum += candles[i].Close;

        return candles[index].Close >= sum / 200;
    }

    private static double? GroupOdds(List<decimal> returns) =>
        returns.Count == 0 ? null : Math.Round((double)returns.Count(r => r > 0) / returns.Count * 100, 1);

    private static decimal? GroupMedian(List<decimal> returns)
    {
        if (returns.Count == 0) return null;
        var sorted = returns.OrderBy(r => r).ToList();
        return sorted[sorted.Count / 2];
    }

    public async Task<AssetPeriodOddsDto> GetOddsForDaysAsync(string symbol, int days, int topK = 10, CancellationToken ct = default)
    {
        if (days is < 7 or > 365)
            throw new ArgumentOutOfRangeException(nameof(days), "days must be between 7 and 365");

        var asset = await _assets.GetBySymbolAsync(symbol.ToUpper(), ct)
            ?? throw new KeyNotFoundException($"Asset {symbol.ToUpper()} not found");

        var matches = await _regime.FindSimilarPeriodsAsync(MatchingOptions.AnalogCount, ct);
        var candles = await _candles.GetDailyHistoryAsync(asset.Id, ct);

        if (matches.Count == 0 || candles.Count == 0)
        {
            return new AssetPeriodOddsDto(
                asset.Symbol, asset.Name, days, 0, null,
                EmptyPeriod(),
                "Insufficient historical data to compute odds.");
        }

        var currentPrice = candles[^1].Close;
        var weighted = ConditionOnAssetState(candles, ApplyKernelWeights(matches));
        var returns = ComputeReturns(candles, weighted, days);

        return new AssetPeriodOddsDto(
            Symbol:      asset.Symbol,
            Name:        asset.Name,
            Days:        days,
            MatchesUsed: matches.Count,
            CurrentPrice: currentPrice,
            Odds:        ComputeOdds(returns, currentPrice, CandleMath.ComputeBaseRate(candles, days)),
            Disclaimer:  Disclaimer
        );
    }

    private static List<(DateTime Date, double Weight)> ApplyKernelWeights(List<HistoricalMatchDto> matches)
    {
        var weights = MacroSnapshotBuilder.KernelWeights(
            matches.Select(m => m.SimilarityScore).ToList());

        return matches
            .Select((m, i) => (m.Date, weights[i]))
            .ToList();
    }

    // Down-weights analogs where the asset's own technical state (trend, drawdown,
    // RSI) differed from today's. A macro match from a month when the asset was
    // mid-crash says little about a day when it sits at all-time highs.
    private static List<(DateTime Date, double Weight)> ConditionOnAssetState(
        List<Candle> candles, List<(DateTime Date, double Weight)> analogs)
    {
        var bandwidth = MatchingOptions.Production.StateBandwidth;
        if (bandwidth is null || candles.Count == 0) return analogs;

        var current = AssetStateCalculator.ComputeAt(candles, candles.Count - 1);
        if (current is null) return analogs; // young asset — not enough history to condition

        var stds = AssetStateCalculator.ComputeStds(
            AssetStateCalculator.SampleMonthlyStates(candles).Select(s => s.State));
        if (!stds.IsUsable) return analogs;

        return analogs.Select(a =>
        {
            var idx = CandleMath.FindNearestIndex(candles, a.Date, 7);
            if (idx < 0) return a;

            var analogState = AssetStateCalculator.ComputeAt(candles, idx);
            if (analogState is null) return a;

            var factor = AssetStateCalculator.StateFactor(current, analogState, stds, bandwidth.Value);
            return (a.Date, a.Weight * factor);
        }).ToList();
    }

    private static List<(decimal Return, double Weight)> ComputeReturns(
        List<Candle> candles, List<(DateTime Date, double Weight)> analogs, int days)
    {
        var window = CandleMath.ExitWindow(days);
        var returns = new List<(decimal, double)>();

        foreach (var (date, weight) in analogs)
        {
            var entry = CandleMath.FindNearest(candles, date, 7);
            if (entry is null) continue;

            var exit = CandleMath.FindNearest(candles, date.AddDays(days), window);
            if (exit is not null && exit.OpenTime > entry.OpenTime)
                returns.Add((CandleMath.PercentReturn(entry.Close, exit.Close), weight));
        }

        return returns;
    }

    // Kernel-weighted analog odds, shrunk toward the asset's base rate.
    // The shrinkage acknowledges that a few dozen analog periods can't support
    // extreme probability claims; the edge (odds - base rate) is what the
    // regime signal actually contributes.
    private static OddsForPeriodDto ComputeOdds(
        List<(decimal Return, double Weight)> returns, decimal currentPrice, double? baseRate)
    {
        if (returns.Count == 0)
            return EmptyPeriod();

        var totalWeight = returns.Sum(r => r.Weight);
        var positiveWeight = returns.Where(r => r.Return > 0).Sum(r => r.Weight);
        var positiveCount = returns.Count(r => r.Return > 0);

        var rawOdds = positiveWeight / totalWeight * 100;

        // Shrink toward the base rate when we know it; without a base rate
        // (very short candle history) fall back to the raw analog odds.
        var shrunkOdds = baseRate is not null
            ? baseRate.Value + MatchingOptions.Shrinkage * (rawOdds - baseRate.Value)
            : rawOdds;

        var sorted = returns.OrderBy(r => r.Return).ToList();

        var weightedAvg = (decimal)(returns.Sum(r => (double)r.Return * r.Weight) / totalWeight);
        var median = WeightedPercentile(sorted, totalWeight, 0.50);
        var p25    = WeightedPercentile(sorted, totalWeight, 0.25);
        var p75    = WeightedPercentile(sorted, totalWeight, 0.75);

        return new OddsForPeriodDto(
            TotalCases:      returns.Count,
            PositiveCases:   positiveCount,
            PositiveOdds:    Math.Round(shrunkOdds, 1),
            AverageReturn:   Math.Round(weightedAvg, 2),
            MedianReturn:    Math.Round(median, 2),
            BestCase:        Math.Round(sorted[^1].Return, 2),
            WorstCase:       Math.Round(sorted[0].Return, 2),
            PriceTargetLow:  Math.Round(currentPrice * (1 + p25 / 100), 2),
            PriceTargetMid:  Math.Round(currentPrice * (1 + median / 100), 2),
            PriceTargetHigh: Math.Round(currentPrice * (1 + p75 / 100), 2),
            BaseRate:        baseRate is null ? null : Math.Round(baseRate.Value, 1),
            Edge:            baseRate is null ? 0 : Math.Round(shrunkOdds - baseRate.Value, 1)
        );
    }

    // Returns the value at which cumulative weight crosses the requested percentile.
    // Input must be sorted by Return ascending.
    private static decimal WeightedPercentile(
        List<(decimal Return, double Weight)> sorted, double totalWeight, double percentile)
    {
        var threshold = totalWeight * percentile;
        var cumulative = 0.0;

        foreach (var (ret, weight) in sorted)
        {
            cumulative += weight;
            if (cumulative >= threshold)
                return ret;
        }

        return sorted[^1].Return;
    }

    private static OddsForPeriodDto EmptyPeriod() =>
        new(0, 0, 0, 0, 0, 0, 0, null, null, null);

    private static AssetOddsDto EmptyOdds(Asset asset) =>
        new(asset.Symbol, asset.Name, 0, null,
            EmptyPeriod(), EmptyPeriod(), EmptyPeriod(),
            [],
            "Insufficient historical data to compute odds. Check back after the first ingestion completes.");
}
