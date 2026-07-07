using SwingSignal.Application.Abstractions.Persistence;
using SwingSignal.Domain.Enums;

namespace SwingSignal.Application.Regime;

public record MonthlySnapshot(DateTime Date, Dictionary<MacroIndicatorType, decimal> Values);

// A selected analog with its standing against the full candidate history:
// Rank 3 of 430 candidates = closer to today than 99% of all months.
public record MatchResult(
    MonthlySnapshot Snapshot,
    double Similarity,
    int Rank,
    int CandidateCount);

// Shared regime-vector math used by both live matching and backtesting.
// Keeping it in one place guarantees the backtest measures exactly what production does.
public class MacroSnapshotBuilder
{
    private readonly IMacroRepository _macro;

    public static readonly MacroIndicatorType[] VectorIndicators =
    [
        MacroIndicatorType.FedFundsRate,
        MacroIndicatorType.UnemploymentRate,
        MacroIndicatorType.CPI,
        MacroIndicatorType.GDP,
        MacroIndicatorType.GoldPrice,
        MacroIndicatorType.OilWTI,
        MacroIndicatorType.TreasuryYield10Y,
        MacroIndicatorType.TreasuryYield2Y,
        MacroIndicatorType.YieldCurveSpread,
        MacroIndicatorType.TreasuryYield3M,
        MacroIndicatorType.YieldSpread10Y3M,
        MacroIndicatorType.FedBalanceSheet,
        MacroIndicatorType.ReverseRepo,
        MacroIndicatorType.RealYield10Y,
        MacroIndicatorType.M2MoneySupply,
        MacroIndicatorType.CorePCE,
        MacroIndicatorType.JoblessClaims,
        MacroIndicatorType.ConsumerSentiment,
        MacroIndicatorType.RetailSales,
        MacroIndicatorType.HousingStarts,
        MacroIndicatorType.HighYieldSpread,
        MacroIndicatorType.SahmRule,
        MacroIndicatorType.VIX,
        MacroIndicatorType.DollarIndex,
        MacroIndicatorType.Copper,
        MacroIndicatorType.CryptoFearGreed,
    ];

    // Trending level series are compared as YoY % change, not raw level.
    // A raw CPI z-score would just measure "how recent is this month" — the
    // meaningful signal is the inflation RATE, not the index level.
    public static readonly HashSet<MacroIndicatorType> YoYTransformed =
    [
        MacroIndicatorType.CPI,
        MacroIndicatorType.GDP,
        MacroIndicatorType.GoldPrice,
        MacroIndicatorType.OilWTI,
        MacroIndicatorType.FedBalanceSheet,
        MacroIndicatorType.M2MoneySupply,
        MacroIndicatorType.CorePCE,
        MacroIndicatorType.RetailSales,
        MacroIndicatorType.HousingStarts,
        MacroIndicatorType.Copper,
    ];

    public const int MinSharedDimensions = 8;
    public const int MinSnapshotIndicators = 8;

    // Matches closer together than this are the same macro event, not independent samples
    public const int MatchSpacingMonths = 6;

    // Indicator families for distance weighting. Without this, five highly
    // correlated rate series would dominate the distance 5-to-1 over, say,
    // the entire labor market. Each family contributes equally.
    private static readonly Dictionary<MacroIndicatorType, int> FamilyOf = new()
    {
        // 0: policy & liquidity
        [MacroIndicatorType.FedFundsRate]      = 0,
        [MacroIndicatorType.RealYield10Y]      = 0,
        [MacroIndicatorType.FedBalanceSheet]   = 0,
        [MacroIndicatorType.M2MoneySupply]     = 0,
        [MacroIndicatorType.ReverseRepo]       = 0,
        // 1: rates & curve
        [MacroIndicatorType.TreasuryYield10Y]  = 1,
        [MacroIndicatorType.TreasuryYield2Y]   = 1,
        [MacroIndicatorType.TreasuryYield3M]   = 1,
        [MacroIndicatorType.YieldCurveSpread]  = 1,
        [MacroIndicatorType.YieldSpread10Y3M]  = 1,
        // 2: inflation
        [MacroIndicatorType.CPI]               = 2,
        [MacroIndicatorType.CorePCE]           = 2,
        // 3: labor
        [MacroIndicatorType.UnemploymentRate]  = 3,
        [MacroIndicatorType.JoblessClaims]     = 3,
        [MacroIndicatorType.SahmRule]          = 3,
        // 4: growth & consumer
        [MacroIndicatorType.GDP]               = 4,
        [MacroIndicatorType.RetailSales]       = 4,
        [MacroIndicatorType.HousingStarts]     = 4,
        [MacroIndicatorType.ConsumerSentiment] = 4,
        // 5: market stress & risk appetite
        [MacroIndicatorType.VIX]               = 5,
        [MacroIndicatorType.HighYieldSpread]   = 5,
        [MacroIndicatorType.DollarIndex]       = 5,
        [MacroIndicatorType.CryptoFearGreed]   = 5,
        // 6: commodities
        [MacroIndicatorType.GoldPrice]         = 6,
        [MacroIndicatorType.OilWTI]            = 6,
        [MacroIndicatorType.Copper]            = 6,

        // Derived momentum dimensions live in their parent's family
        [MacroIndicatorType.FedFundsMomentum6M]        = 0,
        [MacroIndicatorType.UnemploymentMomentum6M]    = 3,
        [MacroIndicatorType.Yield10YMomentum6M]        = 1,
        [MacroIndicatorType.HighYieldSpreadMomentum6M] = 5,
        [MacroIndicatorType.CpiMomentum6M]             = 2,
    };

    // Parent series -> derived 6-month momentum dimension. Momentum is computed
    // on the transformed series (so CPI momentum = change in YoY inflation).
    private static readonly Dictionary<MacroIndicatorType, MacroIndicatorType> MomentumPairs = new()
    {
        [MacroIndicatorType.FedFundsRate]     = MacroIndicatorType.FedFundsMomentum6M,
        [MacroIndicatorType.UnemploymentRate] = MacroIndicatorType.UnemploymentMomentum6M,
        [MacroIndicatorType.TreasuryYield10Y] = MacroIndicatorType.Yield10YMomentum6M,
        [MacroIndicatorType.HighYieldSpread]  = MacroIndicatorType.HighYieldSpreadMomentum6M,
        [MacroIndicatorType.CPI]              = MacroIndicatorType.CpiMomentum6M,
    };

    private const int MomentumMonths = 6;

    // Everything that can appear in a snapshot vector: stored indicators + derived momentum
    public static readonly MacroIndicatorType[] AllDimensions =
        [.. VectorIndicators, .. MomentumPairs.Values];

    private const int MinSharedFamilies = 5;

    public MacroSnapshotBuilder(IMacroRepository macro) => _macro = macro;

    // Builds one snapshot per month using forward-filled values.
    // YoY-transformed indicators are stored as % change vs the same month a year earlier.
    public async Task<List<MonthlySnapshot>> BuildAllAsync(CancellationToken ct = default)
    {
        var allPoints = await _macro.GetForTypesAsync(VectorIndicators, ct);

        if (allPoints.Count == 0) return [];

        var earliest = allPoints.Min(p => p.Date);
        var latest = allPoints.Max(p => p.Date);

        // Step 1: forward-filled monthly raw series per indicator
        var rawSeries = new Dictionary<MacroIndicatorType, SortedDictionary<DateTime, decimal>>();

        foreach (var group in allPoints.GroupBy(p => p.IndicatorType))
        {
            var monthly = new SortedDictionary<DateTime, decimal>();
            var points = group.OrderBy(p => p.Date).ToList();

            var cursor = new DateTime(earliest.Year, earliest.Month, 1);
            var idx = 0;
            decimal? lastValue = null;

            while (cursor <= latest)
            {
                var monthEnd = cursor.AddMonths(1).AddDays(-1);
                while (idx < points.Count && points[idx].Date <= monthEnd)
                {
                    lastValue = points[idx].Value;
                    idx++;
                }

                if (lastValue is not null)
                    monthly[cursor] = lastValue.Value;

                cursor = cursor.AddMonths(1);
            }

            rawSeries[group.Key] = monthly;
        }

        // Step 2: apply YoY transform where needed
        var series = new Dictionary<MacroIndicatorType, Dictionary<DateTime, decimal>>();
        foreach (var (type, monthly) in rawSeries)
        {
            series[type] = YoYTransformed.Contains(type)
                ? ToYoYSeries(monthly)
                : monthly.ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        // Step 2b: derive 6-month momentum dimensions from the transformed series
        foreach (var (parent, momentumType) in MomentumPairs)
        {
            if (!series.TryGetValue(parent, out var parentSeries)) continue;

            var momentum = new Dictionary<DateTime, decimal>();
            foreach (var (month, value) in parentSeries)
            {
                if (parentSeries.TryGetValue(month.AddMonths(-MomentumMonths), out var past))
                    momentum[month] = value - past;
            }

            series[momentumType] = momentum;
        }

        // Step 3: assemble monthly snapshots
        var snapshots = new List<MonthlySnapshot>();
        var month2 = new DateTime(earliest.Year, earliest.Month, 1);

        while (month2 <= latest)
        {
            var values = new Dictionary<MacroIndicatorType, decimal>();

            foreach (var type in AllDimensions)
            {
                if (series.TryGetValue(type, out var s) && s.TryGetValue(month2, out var v))
                    values[type] = v;
            }

            if (values.Count >= MinSnapshotIndicators)
                snapshots.Add(new MonthlySnapshot(month2, values));

            month2 = month2.AddMonths(1);
        }

        return snapshots;
    }

    public static Dictionary<DateTime, decimal> ToYoYSeries(IDictionary<DateTime, decimal> monthly)
    {
        var result = new Dictionary<DateTime, decimal>();

        foreach (var (month, value) in monthly)
        {
            var yearAgo = month.AddMonths(-12);
            if (monthly.TryGetValue(yearAgo, out var baseline) && baseline != 0)
                result[month] = (value - baseline) / Math.Abs(baseline) * 100m;
        }

        return result;
    }

    public static Dictionary<MacroIndicatorType, (decimal Mean, decimal StdDev)> ComputeNormalizationStats(
        IReadOnlyList<MonthlySnapshot> snapshots)
    {
        var stats = new Dictionary<MacroIndicatorType, (decimal Mean, decimal StdDev)>();

        foreach (var type in AllDimensions)
        {
            var values = snapshots
                .Where(s => s.Values.ContainsKey(type))
                .Select(s => s.Values[type])
                .ToList();

            if (values.Count < 12) continue;

            var mean = values.Average();
            var variance = values.Select(v => (v - mean) * (v - mean)).Average();
            var stdDev = (decimal)Math.Sqrt((double)variance);

            if (stdDev > 0)
                stats[type] = (mean, stdDev);
        }

        return stats;
    }

    public static Dictionary<MacroIndicatorType, double> BuildZScoreVector(
        Dictionary<MacroIndicatorType, decimal> values,
        Dictionary<MacroIndicatorType, (decimal Mean, decimal StdDev)> stats)
    {
        var vector = new Dictionary<MacroIndicatorType, double>();

        foreach (var (type, value) in values)
        {
            if (stats.TryGetValue(type, out var stat))
                vector[type] = (double)((value - stat.Mean) / stat.StdDev);
        }

        return vector;
    }

    // Gaussian kernel weights from similarity scores: close analogs dominate,
    // far ones fade smoothly instead of a hard top-K cutoff.
    // Similarity = 100/(1+d)  =>  d = 100/similarity - 1. Bandwidth = median distance.
    public static double[] KernelWeights(IReadOnlyList<double> similarities)
    {
        var distances = similarities
            .Select(s => 100.0 / Math.Max(s, 1e-6) - 1.0)
            .ToArray();

        var median = distances.OrderBy(d => d).ElementAt(distances.Length / 2);
        var bandwidth = Math.Max(median, 1e-6);

        return distances
            .Select(d => Math.Exp(-Math.Pow(d / bandwidth, 2)))
            .ToArray();
    }

    // Family-weighted RMS distance over the indicators both vectors share.
    // Each indicator family contributes equally regardless of how many of its
    // members happen to be present. With familyWeighting off, falls back to a
    // flat RMS over all shared indicators (the pre-improvement behavior).
    public static double? SharedDimensionDistance(
        Dictionary<MacroIndicatorType, double> a,
        Dictionary<MacroIndicatorType, double> b,
        bool familyWeighting = true)
    {
        var familySumSq = new Dictionary<int, double>();
        var familyCount = new Dictionary<int, int>();
        var shared = 0;
        var flatSumSq = 0.0;

        foreach (var (type, valueA) in a)
        {
            if (!b.TryGetValue(type, out var valueB)) continue;
            if (!FamilyOf.TryGetValue(type, out var family)) continue;

            var diff = valueA - valueB;
            familySumSq[family] = familySumSq.GetValueOrDefault(family) + diff * diff;
            familyCount[family] = familyCount.GetValueOrDefault(family) + 1;
            flatSumSq += diff * diff;
            shared++;
        }

        if (shared < MinSharedDimensions || familyCount.Count < MinSharedFamilies)
            return null;

        if (!familyWeighting)
            return Math.Sqrt(flatSumSq / shared);

        // Mean squared diff within each family, then RMS across families
        var acrossFamilies = familySumSq.Keys
            .Average(f => familySumSq[f] / familyCount[f]);

        return Math.Sqrt(acrossFamilies);
    }

    // Finds the topK most similar snapshots to the one at [asOfIndex], using only
    // information available at that time (stats from snapshots[0..asOfIndex]).
    // Candidates must be at least 6 months older than the target month, and
    // selected matches must be at least MatchSpacingMonths apart so that one
    // macro event (e.g. late 2008) can't occupy several slots.
    public static List<MatchResult> FindMatches(
        IReadOnlyList<MonthlySnapshot> snapshots, int asOfIndex, int topK,
        MatchingOptions? options = null)
    {
        options ??= MatchingOptions.Production;

        var visible = snapshots.Take(asOfIndex + 1).ToList();
        var stats = ComputeNormalizationStats(visible);
        var currentVector = BuildZScoreVector(snapshots[asOfIndex].Values, stats);

        if (currentVector.Count < MinSharedDimensions) return [];

        var cutoff = snapshots[asOfIndex].Date.AddMonths(-6);
        var candidates = new List<(MonthlySnapshot Snapshot, double Similarity)>();

        foreach (var candidate in visible.Where(s => s.Date <= cutoff))
        {
            var vector = BuildZScoreVector(candidate.Values, stats);
            var distance = SharedDimensionDistance(currentVector, vector, options.FamilyWeighting);
            if (distance is null) continue;

            candidates.Add((candidate, 100.0 / (1.0 + distance.Value)));
        }

        var ordered = candidates.OrderByDescending(c => c.Similarity).ToList();
        var total = ordered.Count;

        if (!options.Decluster)
        {
            return ordered
                .Take(topK)
                .Select((c, i) => new MatchResult(c.Snapshot, c.Similarity, i + 1, total))
                .ToList();
        }

        // Greedy selection: best first, skip anything too close to an already-picked match.
        // Rank reflects the candidate's position in the FULL sorted history, so a
        // declustered representative keeps its true standing.
        var selected = new List<MatchResult>();

        for (var i = 0; i < ordered.Count && selected.Count < topK; i++)
        {
            var candidate = ordered[i];

            var tooClose = selected.Any(s =>
                Math.Abs((s.Snapshot.Date - candidate.Snapshot.Date).TotalDays) < MatchSpacingMonths * 30);

            if (!tooClose)
                selected.Add(new MatchResult(candidate.Snapshot, candidate.Similarity, i + 1, total));
        }

        return selected;
    }
}
