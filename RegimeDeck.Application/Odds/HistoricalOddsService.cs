using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Application.Common;
using RegimeDeck.Application.Regime;
using RegimeDeck.Contracts.Regime;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Application.Odds;

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

    public async Task<AssetOddsDto> GetOddsAsync(string symbol, CancellationToken ct = default)
    {
        var asset = await _assets.GetBySymbolAsync(symbol.ToUpperInvariant(), ct)
            ?? throw new NotFoundException($"Asset {symbol.ToUpperInvariant()} not found");

        var candles = await _candles.GetDailyHistoryAsync(asset.Id, ct);

        // Horizon-split profiles: short windows and long windows want
        // different analog evidence (crypto: cycle gauges vs macro depth).
        // For non-crypto both profiles are the same instance — one load.
        var longOptions = MatchingOptions.ForMarket(asset.MarketType, 90);
        var shortOptions = MatchingOptions.ForMarket(asset.MarketType, 30);

        var (matches, weighted) = await LoadAnalogsAsync(candles, longOptions, ct);
        var (_, shortWeighted) = ReferenceEquals(shortOptions, longOptions)
            ? (matches, weighted)
            : await LoadAnalogsAsync(candles, shortOptions, ct);

        if (matches.Count == 0 || candles.Count == 0)
            return EmptyOdds(asset);

        var currentPrice = candles[^1].Close;
        var explanations = await _explainer.GenerateAsync(asset.Symbol, asset.MarketType, matches, candles, ct);

        var oneMonth    = ComputeOdds(ComputeReturns(candles, shortWeighted, 30), BaseRateFor(candles, 30, shortOptions), shortOptions);
        var threeMonths = ComputeOdds(ComputeReturns(candles, weighted, 90), BaseRateFor(candles, 90, longOptions), longOptions);
        var sixMonths   = ComputeOdds(ComputeReturns(candles, weighted, 180), BaseRateFor(candles, 180, longOptions), longOptions);

        return new AssetOddsDto(
            Symbol:      asset.Symbol,
            Name:        asset.Name,
            MatchesUsed: matches.Count,
            CurrentPrice: currentPrice,
            OneMonth:    oneMonth,
            ThreeMonths: threeMonths,
            SixMonths:   sixMonths,
            Explanations: explanations,
            Disclaimer:  Disclaimer,
            Breakdown:   ComputeBreakdown(candles, matches),
            TradeRead:   TradeRead.Compute(oneMonth, threeMonths, sixMonths, matches.Count)
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

    // The profile decides whether the base rate spans all history or only
    // the trailing window (crypto: early bull-heavy years distort "normal")
    private static double? BaseRateFor(List<Candle> candles, int days, MatchingOptions options) =>
        CandleMath.ComputeBaseRate(candles, days,
            options.BaseRateTrailingYears is int years && candles.Count > 0
                ? candles[^1].OpenTime.AddYears(-years)
                : null);

    // Analogs matched under one profile, kernel-weighted and conditioned
    private async Task<(List<HistoricalMatchDto> Matches, List<(DateTime Date, double Weight)> Weighted)>
        LoadAnalogsAsync(List<Candle> candles, MatchingOptions options, CancellationToken ct)
    {
        var matches = await _regime.FindSimilarPeriodsAsync(
            MatchingOptions.AnalogCount, options, MinAnalogDate(options, candles), ct);

        var weighted = matches.Count == 0 || candles.Count == 0
            ? []
            : ConditionOnCryptoCycle(options, candles,
                ConditionOnAssetState(candles, ApplyKernelWeights(matches)));

        return (matches, weighted);
    }

    // The candidate floor travels with the crypto profile: analogs before the
    // asset's first candle can never be scored, so for short-history assets
    // they only shrink the effective sample.
    private static DateTime? MinAnalogDate(MatchingOptions options, List<Candle> candles) =>
        options.FloorAnalogsToAssetHistory && candles.Count > 0
            ? candles[0].OpenTime
            : null;

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

    // Down-weights analogs from a different point in the crypto cycle (halving
    // phase + Mayer multiple). Only active when the profile carries a bandwidth.
    private static List<(DateTime Date, double Weight)> ConditionOnCryptoCycle(
        MatchingOptions options, List<Candle> candles, List<(DateTime Date, double Weight)> analogs)
    {
        var bandwidth = options.CryptoCycleBandwidth;
        if (bandwidth is null || candles.Count == 0) return analogs;

        var now = candles[^1].OpenTime;
        var mayerNow = CryptoCycle.MayerMultipleAt(candles, candles.Count - 1);

        return analogs.Select(a =>
        {
            var idx = CandleMath.FindNearestIndex(candles, a.Date, 7);
            var mayerAnalog = idx >= 0 ? CryptoCycle.MayerMultipleAt(candles, idx) : null;
            var factor = CryptoCycle.CycleFactor(now, mayerNow, a.Date, mayerAnalog, bandwidth.Value);
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
    // regime signal actually contributes. With a ShrinkagePrior set, the
    // shrinkage scales with the Kish effective sample size, so assets with
    // thin history automatically publish humbler odds.
    private static OddsForPeriodDto ComputeOdds(
        List<(decimal Return, double Weight)> returns, double? baseRate, MatchingOptions options)
    {
        if (returns.Count == 0)
            return EmptyPeriod();

        var totalWeight = returns.Sum(r => r.Weight);
        var positiveWeight = returns.Where(r => r.Return > 0).Sum(r => r.Weight);
        var positiveCount = returns.Count(r => r.Return > 0);

        var rawOdds = positiveWeight / totalWeight * 100;

        // Shrink toward the base rate when we know it; without a base rate
        // (very short candle history) fall back to the raw analog odds.
        var shrunkOdds = baseRate is null
            ? rawOdds
            : options.ShrinkagePrior is double prior
                ? OddsMath.AdaptiveShrink(
                    rawOdds, baseRate.Value,
                    OddsMath.EffectiveSampleSize(returns.Select(r => r.Weight).ToList()), prior)
                : baseRate.Value + MatchingOptions.Shrinkage * (rawOdds - baseRate.Value);

        var sorted = returns.OrderBy(r => r.Return).ToList();

        var weightedAvg = (decimal)(returns.Sum(r => (double)r.Return * r.Weight) / totalWeight);
        var median = WeightedPercentile(sorted, totalWeight, 0.50);

        return new OddsForPeriodDto(
            TotalCases:      returns.Count,
            PositiveCases:   positiveCount,
            PositiveOdds:    Math.Round(shrunkOdds, 1),
            AverageReturn:   Math.Round(weightedAvg, 2),
            MedianReturn:    Math.Round(median, 2),
            BestCase:        Math.Round(sorted[^1].Return, 2),
            WorstCase:       Math.Round(sorted[0].Return, 2),
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

    private static OddsForPeriodDto EmptyPeriod() => new(0, 0, 0, 0, 0, 0, 0);

    private static AssetOddsDto EmptyOdds(Asset asset) =>
        new(asset.Symbol, asset.Name, 0, null,
            EmptyPeriod(), EmptyPeriod(), EmptyPeriod(),
            [],
            "Insufficient historical data to compute odds. Check back after the first ingestion completes.");
}
