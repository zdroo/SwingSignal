using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Application.Common;
using RegimeDeck.Application.Odds;
using RegimeDeck.Application.Regime;
using RegimeDeck.Contracts.Backtests;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Application.Backtesting;

// Walk-forward calibration backtest.
//
// For each historical month, it reproduces exactly what production would have
// predicted AT THAT TIME (normalization stats and candidate matches use only
// prior data — no lookahead), then compares the predicted odds against what
// the asset actually did over the following N days.
public class BacktestService : IBacktestService
{
    // Require this many months of history before making the first prediction
    private const int WarmupMonths = 60;
    // A prediction needs at least this many usable historical returns
    private const int MinUsableMatches = 5;

    private readonly IAssetRepository _assets;
    private readonly ICandleRepository _candles;
    private readonly MacroSnapshotBuilder _snapshots;

    public BacktestService(
        IAssetRepository assets,
        ICandleRepository candles,
        MacroSnapshotBuilder snapshots)
    {
        _assets = assets;
        _candles = candles;
        _snapshots = snapshots;
    }

    public async Task<BacktestResultDto> RunAsync(
        string symbol, int days, int topK = 10,
        int? fromYear = null, int? toYear = null, double? stateBandwidth = null,
        string? profile = null, bool? floorHistory = null, double? cycleBandwidth = null,
        double? shrinkPrior = null, int? baseRateYears = null,
        CancellationToken ct = default)
    {
        var (asset, snapshots, candles) = await LoadDataAsync(symbol, days, ct);

        var baseOptions = profile switch
        {
            "crypto"        => MatchingOptions.CryptoProduction,
            "crypto-native" => MatchingOptions.CryptoShortHorizon,
            "default"       => MatchingOptions.Production,
            _               => MatchingOptions.ForMarket(asset.MarketType, days),
        };

        // An explicit 0 disables conditioning; null keeps the production setting
        var options = baseOptions with
        {
            StateBandwidth = stateBandwidth is null
                ? baseOptions.StateBandwidth
                : stateBandwidth == 0 ? null : stateBandwidth,
            CryptoCycleBandwidth = cycleBandwidth is null
                ? baseOptions.CryptoCycleBandwidth
                : cycleBandwidth == 0 ? null : cycleBandwidth,
            ShrinkagePrior = shrinkPrior is null
                ? baseOptions.ShrinkagePrior
                : shrinkPrior == 0 ? null : shrinkPrior,
            BaseRateTrailingYears = baseRateYears is null
                ? baseOptions.BaseRateTrailingYears
                : baseRateYears == 0 ? null : baseRateYears
        };

        return RunCore(asset, snapshots, candles, days, topK, options, fromYear, toYear,
            AnalogFloor(floorHistory ?? baseOptions.FloorAnalogsToAssetHistory, candles));
    }

    public async Task<BacktestComparisonDto> CompareAsync(
        string symbol, int days, int topK = 10,
        int? fromYear = null, int? toYear = null,
        CancellationToken ct = default)
    {
        var (asset, snapshots, candles) = await LoadDataAsync(symbol, days, ct);

        // "current" mirrors production exactly: crypto assets use the
        // horizon-appropriate crypto profile, everything else the full set.
        var currentOptions = MatchingOptions.ForMarket(asset.MarketType, days);

        var baseline = RunCore(asset, snapshots, candles, days, topK, MatchingOptions.Baseline, fromYear, toYear);
        var current = RunCore(asset, snapshots, candles, days, topK, currentOptions, fromYear, toYear,
            AnalogFloor(currentOptions.FloorAnalogsToAssetHistory, candles));

        return new BacktestComparisonDto(baseline, current, Summarize(baseline, current));
    }

    // Analogs before the asset's first candle can never be scored — the floor
    // trades them for scoreable ones when a profile (or caller) asks for it.
    private static DateTime? AnalogFloor(bool apply, List<Candle> candles) =>
        apply && candles.Count > 0 ? candles[0].OpenTime : null;

    private async Task<(Asset Asset, List<MonthlySnapshot> Snapshots, List<Candle> Candles)> LoadDataAsync(
        string symbol, int days, CancellationToken ct)
    {
        if (days is < 7 or > 365)
            throw new ValidationException("days must be between 7 and 365");

        var asset = await _assets.GetBySymbolAsync(symbol.ToUpperInvariant(), ct)
            ?? throw new NotFoundException($"Asset {symbol.ToUpperInvariant()} not found");

        var snapshots = await _snapshots.BuildAllAsync(ct);
        var candles = await _candles.GetDailyHistoryAsync(asset.Id, ct);

        return (asset, snapshots, candles);
    }

    private static BacktestResultDto RunCore(
        Asset asset,
        List<MonthlySnapshot> snapshots,
        List<Candle> candles,
        int days,
        int topK,
        MatchingOptions options,
        int? fromYear = null,
        int? toYear = null,
        DateTime? minAnalogDate = null)
    {
        var records = new List<(double PredictedOdds, bool ActualPositive)>();
        DateTime? firstDate = null, lastDate = null;

        var exitWindow = CandleMath.ExitWindow(days);
        var horizonEnd = DateTime.UtcNow.AddDays(-days);
        var analogCount = options.KernelAllHistory ? MatchingOptions.AnalogCount : topK;

        // Pre-sampled outcomes for the walk-forward base rate: only windows whose
        // outcome was observable before the evaluation month may contribute.
        // With a trailing window, samples older than the window fall out again
        // (two pointers over the exit-date-ordered list — still no lookahead).
        var baseRateSamples = CandleMath.SampleOutcomes(candles, days);
        var baseRateIdx = 0;
        var baseRateStart = 0;
        var baseRatePositive = 0;

        // Pre-sampled asset states for walk-forward normalization of the
        // state-conditioning distance (expanding window, no lookahead).
        var stateSamples = options.StateBandwidth is not null
            ? AssetStateCalculator.SampleMonthlyStates(candles)
            : [];
        var stateIdx = 0;
        var visibleStates = new List<AssetState>();

        for (var i = WarmupMonths; i < snapshots.Count; i++)
        {
            var evalMonth = snapshots[i].Date;

            // The outcome must already be observable
            if (evalMonth > horizonEnd) break;
            if (toYear is not null && evalMonth.Year >= toYear) break;
            if (fromYear is not null && evalMonth.Year < fromYear) continue;

            // The asset must have been tradable at this month
            var entryIdx = CandleMath.FindNearestIndex(candles, evalMonth, 7);
            if (entryIdx < 0) continue;
            var actualEntry = candles[entryIdx];

            var actualExit = CandleMath.FindNearest(candles, evalMonth.AddDays(days), exitWindow);
            if (actualExit is null || actualExit.OpenTime <= actualEntry.OpenTime) continue;

            // Advance the walk-forward base rate to what was knowable at evalMonth
            while (baseRateIdx < baseRateSamples.Count &&
                   baseRateSamples[baseRateIdx].ExitDate <= evalMonth)
            {
                if (baseRateSamples[baseRateIdx].Positive) baseRatePositive++;
                baseRateIdx++;
            }

            if (options.BaseRateTrailingYears is int trailingYears)
            {
                var windowStart = evalMonth.AddYears(-trailingYears);
                while (baseRateStart < baseRateIdx &&
                       baseRateSamples[baseRateStart].ExitDate < windowStart)
                {
                    if (baseRateSamples[baseRateStart].Positive) baseRatePositive--;
                    baseRateStart++;
                }
            }

            var baseRateCount = baseRateIdx - baseRateStart;
            double? baseRate = baseRateCount >= CandleMath.MinBaseRateSamples
                ? (double)baseRatePositive / baseRateCount * 100
                : null;

            // Advance the walk-forward state normalization stats
            while (stateIdx < stateSamples.Count && stateSamples[stateIdx].Date <= evalMonth)
            {
                visibleStates.Add(stateSamples[stateIdx].State);
                stateIdx++;
            }

            var currentState = options.StateBandwidth is not null
                ? AssetStateCalculator.ComputeAt(candles, entryIdx)
                : null;

            var stateStds = currentState is not null
                ? AssetStateCalculator.ComputeStds(visibleStates)
                : null;

            var conditionState = currentState is not null && stateStds is not null && stateStds.IsUsable;

            // What the algorithm (under these options) would have predicted at this time
            var matches = MacroSnapshotBuilder.FindMatches(snapshots, i, analogCount, options, minAnalogDate);
            if (matches.Count == 0) continue;

            var kernelWeights = options.KernelAllHistory
                ? MacroSnapshotBuilder.KernelWeights(matches.Select(m => m.Similarity).ToList())
                : null;

            var usable = 0;
            var totalWeight = 0.0;
            var positiveWeight = 0.0;
            var usedWeights = new List<double>();

            for (var m = 0; m < matches.Count; m++)
            {
                var match = matches[m].Snapshot;
                var similarity = matches[m].Similarity;

                var analogIdx = CandleMath.FindNearestIndex(candles, match.Date, 7);
                if (analogIdx < 0) continue;
                var entry = candles[analogIdx];

                var exit = CandleMath.FindNearest(candles, match.Date.AddDays(days), exitWindow);
                if (exit is null || exit.OpenTime <= entry.OpenTime) continue;

                var weight = kernelWeights is not null ? kernelWeights[m]
                    : options.SimilarityWeighting ? similarity
                    : 1.0;

                if (conditionState)
                {
                    var analogState = AssetStateCalculator.ComputeAt(candles, analogIdx);
                    if (analogState is not null)
                        weight *= AssetStateCalculator.StateFactor(
                            currentState!, analogState, stateStds!, options.StateBandwidth!.Value);
                }

                if (options.CryptoCycleBandwidth is not null)
                {
                    weight *= CryptoCycle.CycleFactor(
                        evalMonth, CryptoCycle.MayerMultipleAt(candles, entryIdx),
                        match.Date, CryptoCycle.MayerMultipleAt(candles, analogIdx),
                        options.CryptoCycleBandwidth.Value);
                }

                usable++;
                totalWeight += weight;
                usedWeights.Add(weight);
                if (exit.Close > entry.Close)
                    positiveWeight += weight;
            }

            if (usable < MinUsableMatches || totalWeight < 1e-9) continue;

            var predictedOdds = positiveWeight / totalWeight * 100;

            if (options.ShrinkToBaseRate && baseRate is not null)
            {
                predictedOdds = options.ShrinkagePrior is double prior
                    ? OddsMath.AdaptiveShrink(
                        predictedOdds, baseRate.Value, OddsMath.EffectiveSampleSize(usedWeights), prior)
                    : baseRate.Value + MatchingOptions.Shrinkage * (predictedOdds - baseRate.Value);
            }

            var actualPositive = actualExit.Close > actualEntry.Close;

            records.Add((predictedOdds, actualPositive));
            firstDate ??= evalMonth;
            lastDate = evalMonth;
        }

        if (records.Count == 0)
        {
            return new BacktestResultDto(
                asset.Symbol, asset.Name, days, topK,
                0, DateTime.MinValue, DateTime.MinValue,
                0, 0, 0, 0, [],
                "Not enough overlapping macro and price history to run a backtest for this asset and period.",
                Disclaimer());
        }

        // Directional accuracy: prediction is "up" when odds >= 50
        var correct = records.Count(r => (r.PredictedOdds >= 50) == r.ActualPositive);
        var directionalAccuracy = (double)correct / records.Count * 100;

        // Brier score on the probability itself (0 = perfect, 0.25 = always saying 50%)
        var brier = records.Average(r =>
        {
            var p = r.PredictedOdds / 100.0;
            var outcome = r.ActualPositive ? 1.0 : 0.0;
            return (p - outcome) * (p - outcome);
        });

        var calibration = BuildCalibration(records);
        var avgPredicted = records.Average(r => r.PredictedOdds);
        var actualRate = (double)records.Count(r => r.ActualPositive) / records.Count * 100;

        return new BacktestResultDto(
            Symbol: asset.Symbol,
            Name: asset.Name,
            Days: days,
            TopK: topK,
            TotalPredictions: records.Count,
            FirstPrediction: firstDate!.Value,
            LastPrediction: lastDate!.Value,
            DirectionalAccuracy: Math.Round(directionalAccuracy, 1),
            BrierScore: Math.Round(brier, 4),
            AvgPredictedOdds: Math.Round(avgPredicted, 1),
            ActualPositiveRate: Math.Round(actualRate, 1),
            Calibration: calibration,
            Interpretation: Interpret(records.Count, directionalAccuracy, brier, calibration),
            Disclaimer: Disclaimer());
    }

    private static string Summarize(BacktestResultDto baseline, BacktestResultDto current)
    {
        if (baseline.TotalPredictions == 0 || current.TotalPredictions == 0)
            return "Not enough data to compare configurations.";

        var brierDelta = current.BrierScore - baseline.BrierScore;
        var accuracyDelta = current.DirectionalAccuracy - baseline.DirectionalAccuracy;

        var brierVerdict = brierDelta < -0.005 ? "improves"
            : brierDelta > 0.005 ? "worsens"
            : "barely changes";

        return $"The current algorithm (de-clustered analogs, kernel weighting over all history, " +
               $"family-weighted distance, momentum dimensions, base-rate shrinkage) " +
               $"{brierVerdict} probability quality vs the naive baseline: " +
               $"Brier {baseline.BrierScore:F3} → {current.BrierScore:F3} ({(brierDelta >= 0 ? "+" : "")}{brierDelta:F3}), " +
               $"directional accuracy {baseline.DirectionalAccuracy:F0}% → {current.DirectionalAccuracy:F0}% " +
               $"({(accuracyDelta >= 0 ? "+" : "")}{accuracyDelta:F0}pp). " +
               "Lower Brier is better; improvements should hold across several assets before trusting them.";
    }

    private static List<BacktestBucketDto> BuildCalibration(
        List<(double PredictedOdds, bool ActualPositive)> records)
    {
        (double Min, double Max, string Label)[] buckets =
        [
            (0, 40, "< 40%"),
            (40, 50, "40-50%"),
            (50, 60, "50-60%"),
            (60, 70, "60-70%"),
            (70, 80, "70-80%"),
            (80, 101, "80%+"),
        ];

        var result = new List<BacktestBucketDto>();

        foreach (var (min, max, label) in buckets)
        {
            var inBucket = records.Where(r => r.PredictedOdds >= min && r.PredictedOdds < max).ToList();
            if (inBucket.Count == 0) continue;

            result.Add(new BacktestBucketDto(
                PredictedRange: label,
                Predictions: inBucket.Count,
                AvgPredictedOdds: Math.Round(inBucket.Average(r => r.PredictedOdds), 1),
                ActualPositiveRate: Math.Round(
                    (double)inBucket.Count(r => r.ActualPositive) / inBucket.Count * 100, 1)));
        }

        return result;
    }

    private static string Interpret(int n, double accuracy, double brier, List<BacktestBucketDto> calibration)
    {
        if (n < 30)
            return $"Only {n} test points — treat these numbers as indicative, not statistically solid. " +
                   "More price history (older assets) gives more reliable calibration.";

        // Average absolute gap between predicted odds and actual rate, weighted by bucket size
        var totalWeight = calibration.Sum(b => b.Predictions);
        var calibrationGap = calibration.Sum(b =>
            Math.Abs(b.AvgPredictedOdds - b.ActualPositiveRate) * b.Predictions) / totalWeight;

        var calibrationVerdict = calibrationGap switch
        {
            < 10 => "well calibrated — when the system says 70%, reality lands close to 70%",
            < 20 => "moderately calibrated — directionally useful but the exact percentages overstate precision",
            _    => "poorly calibrated — the percentages should not be taken at face value for this asset/period"
        };

        var brierVerdict = brier < 0.20 ? "better than a coin flip"
            : brier <= 0.26 ? "roughly coin-flip level"
            : "worse than a coin flip";

        return $"Across {n} walk-forward predictions the system was {calibrationVerdict}. " +
               $"Probability quality (Brier {brier:F3}) is {brierVerdict}; directional accuracy {accuracy:F0}%. " +
               "Note: monthly predictions with overlapping horizons are correlated, so effective sample size is smaller than the raw count.";
    }

    private static string Disclaimer() =>
        "Backtest of the matching algorithm on historical data. In-sample by construction for indicator selection; " +
        "results do not guarantee future performance. Not financial advice.";
}
