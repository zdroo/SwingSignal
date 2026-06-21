using Microsoft.EntityFrameworkCore;
using SwingSignal.Application.DTOs;
using SwingSignal.Application.Interfaces;
using SwingSignal.Domain.Enums;

namespace SwingSignal.Infrastructure.Services;

public class MacroRegimeService : IMacroRegimeService
{
    private readonly SwingSignalDbContext _db;

    // Indicators used to build the regime vector
    private static readonly MacroIndicatorType[] VectorIndicators =
    [
        MacroIndicatorType.FedFundsRate,
        MacroIndicatorType.UnemploymentRate,
        MacroIndicatorType.CPI,
        MacroIndicatorType.GoldPrice,
        MacroIndicatorType.OilWTI,
        MacroIndicatorType.TreasuryYield10Y,
        MacroIndicatorType.TreasuryYield2Y,
        MacroIndicatorType.YieldCurveSpread,
    ];

    public MacroRegimeService(SwingSignalDbContext db) => _db = db;

    public async Task<MacroRegimeDto> GetCurrentRegimeAsync(CancellationToken ct = default)
    {
        var indicators = new Dictionary<string, MacroIndicatorValueDto>();

        foreach (var type in VectorIndicators)
        {
            var latest = await _db.MacroDataPoints
                .Where(m => m.IndicatorType == type)
                .OrderByDescending(m => m.Date)
                .Take(2)
                .ToListAsync(ct);

            if (latest.Count == 0) continue;

            var current = latest[0];
            var previous = latest.Count > 1 ? latest[1] : null;

            var trend = previous is null ? "Stable"
                : current.Value > previous.Value * 1.001m ? "Rising"
                : current.Value < previous.Value * 0.999m ? "Falling"
                : "Stable";

            var signal = ClassifySignal(type, current.Value);

            indicators[type.ToString()] = new MacroIndicatorValueDto(current.Value, signal, trend);
        }

        var asOf = indicators.Count > 0
            ? await _db.MacroDataPoints.MaxAsync(m => m.Date, ct)
            : DateTime.UtcNow;

        return new MacroRegimeDto(indicators, asOf);
    }

    public async Task<List<HistoricalMatchDto>> FindSimilarPeriodsAsync(int topK = 10, CancellationToken ct = default)
    {
        var snapshots = await BuildHistoricalSnapshotsAsync(ct);

        if (snapshots.Count < 2)
            return [];

        var stats = ComputeNormalizationStats(snapshots);
        var currentVector = BuildCurrentVector(snapshots[^1].Values, stats);

        var similarities = new List<(DateTime Date, double Score, Dictionary<MacroIndicatorType, decimal> Values)>();

        // Skip the last 6 months to avoid comparing current to near-current
        var cutoff = snapshots[^1].Date.AddMonths(-6);

        foreach (var snapshot in snapshots.Where(s => s.Date <= cutoff))
        {
            var historicalVector = BuildCurrentVector(snapshot.Values, stats);

            if (historicalVector.Length == 0) continue;

            var distance = EuclideanDistance(currentVector, historicalVector);
            var similarity = 100.0 / (1.0 + distance);

            similarities.Add((snapshot.Date, similarity, snapshot.Values));
        }

        return similarities
            .OrderByDescending(s => s.Score)
            .Take(topK)
            .Select(s => new HistoricalMatchDto(
                s.Date,
                Math.Round(s.Score, 1),
                s.Values.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value)))
            .ToList();
    }

    // Builds one snapshot per month using forward-filled values
    private async Task<List<MonthlySnapshot>> BuildHistoricalSnapshotsAsync(CancellationToken ct)
    {
        var allPoints = await _db.MacroDataPoints
            .Where(m => VectorIndicators.Contains(m.IndicatorType))
            .OrderBy(m => m.Date)
            .ToListAsync(ct);

        if (allPoints.Count == 0) return [];

        var byType = allPoints
            .GroupBy(p => p.IndicatorType)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Date).ToList());

        var earliest = allPoints.Min(p => p.Date);
        var latest = allPoints.Max(p => p.Date);

        var snapshots = new List<MonthlySnapshot>();
        var cursor = new DateTime(earliest.Year, earliest.Month, 1);

        // Last known value per indicator — forward fill
        var lastKnown = new Dictionary<MacroIndicatorType, decimal>();

        while (cursor <= latest)
        {
            foreach (var type in VectorIndicators)
            {
                if (!byType.TryGetValue(type, out var points)) continue;

                var pointsUpToNow = points.Where(p => p.Date <= cursor.AddMonths(1).AddDays(-1)).ToList();
                if (pointsUpToNow.Count > 0)
                    lastKnown[type] = pointsUpToNow[^1].Value;
            }

            // Only include months where we have at least 5 of the 8 indicators
            if (lastKnown.Count >= 5)
            {
                snapshots.Add(new MonthlySnapshot(cursor, new Dictionary<MacroIndicatorType, decimal>(lastKnown)));
            }

            cursor = cursor.AddMonths(1);
        }

        return snapshots;
    }

    private static Dictionary<MacroIndicatorType, (decimal Mean, decimal StdDev)> ComputeNormalizationStats(
        List<MonthlySnapshot> snapshots)
    {
        var stats = new Dictionary<MacroIndicatorType, (decimal Mean, decimal StdDev)>();

        foreach (var type in VectorIndicators)
        {
            var values = snapshots
                .Where(s => s.Values.ContainsKey(type))
                .Select(s => s.Values[type])
                .ToList();

            if (values.Count < 2) continue;

            var mean = values.Average();
            var variance = values.Select(v => (v - mean) * (v - mean)).Average();
            var stdDev = (decimal)Math.Sqrt((double)variance);

            if (stdDev > 0)
                stats[type] = (mean, stdDev);
        }

        return stats;
    }

    private static double[] BuildCurrentVector(
        Dictionary<MacroIndicatorType, decimal> values,
        Dictionary<MacroIndicatorType, (decimal Mean, decimal StdDev)> stats)
    {
        var components = new List<double>();

        foreach (var type in VectorIndicators)
        {
            if (!values.TryGetValue(type, out var val)) continue;
            if (!stats.TryGetValue(type, out var stat)) continue;

            var normalized = (double)((val - stat.Mean) / stat.StdDev);
            components.Add(normalized);
        }

        return [.. components];
    }

    private static double EuclideanDistance(double[] a, double[] b)
    {
        var len = Math.Min(a.Length, b.Length);
        var sum = 0.0;
        for (int i = 0; i < len; i++)
            sum += (a[i] - b[i]) * (a[i] - b[i]);
        return Math.Sqrt(sum);
    }

    private static string ClassifySignal(MacroIndicatorType type, decimal value) => type switch
    {
        MacroIndicatorType.FedFundsRate     => value > 4m ? "Restrictive" : value > 2m ? "Neutral" : "Accommodative",
        MacroIndicatorType.UnemploymentRate => value > 6m ? "Elevated" : value > 4m ? "Neutral" : "Healthy",
        MacroIndicatorType.CPI              => value > 4m ? "Elevated" : value > 2m ? "Neutral" : "Low",
        MacroIndicatorType.YieldCurveSpread => value < 0m ? "Inverted" : value < 0.5m ? "Flat" : "Normal",
        MacroIndicatorType.GoldPrice        => value > 2000m ? "Risk-off" : value > 1500m ? "Neutral" : "Risk-on",
        MacroIndicatorType.OilWTI           => value > 90m ? "Expensive" : value > 60m ? "Neutral" : "Cheap",
        MacroIndicatorType.TreasuryYield10Y => value > 4m ? "High" : value > 2m ? "Neutral" : "Low",
        MacroIndicatorType.TreasuryYield2Y  => value > 4m ? "High" : value > 2m ? "Neutral" : "Low",
        _                                   => "Neutral"
    };

    private record MonthlySnapshot(DateTime Date, Dictionary<MacroIndicatorType, decimal> Values);
}
