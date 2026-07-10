using SwingSignal.Application.Abstractions.Persistence;
using SwingSignal.Contracts.Regime;
using SwingSignal.Domain.Enums;

namespace SwingSignal.Application.Regime;

public class MacroRegimeService : IMacroRegimeService
{
    private readonly IMacroRepository _macro;
    private readonly MacroSnapshotBuilder _snapshots;

    public MacroRegimeService(IMacroRepository macro, MacroSnapshotBuilder snapshots)
    {
        _macro = macro;
        _snapshots = snapshots;
    }

    public async Task<MacroRegimeDto> GetCurrentRegimeAsync(CancellationToken ct = default)
    {
        var indicators = new Dictionary<string, MacroIndicatorValueDto>();

        foreach (var type in MacroSnapshotBuilder.VectorIndicators)
        {
            // Load ~14 months so YoY indicators have a year-ago baseline
            var cutoff = DateTime.UtcNow.AddDays(-430);
            var points = await _macro.GetSinceAsync(type, cutoff, ct);

            if (points.Count == 0) continue;

            // One value per month (last observation wins)
            var monthly = points
                .GroupBy(p => new DateTime(p.Date.Year, p.Date.Month, 1))
                .ToDictionary(g => g.Key, g => g.Last().Value);

            var displayed = MacroSnapshotBuilder.YoYTransformed.Contains(type)
                ? MacroSnapshotBuilder.ToYoYSeries(monthly)
                : monthly;

            if (displayed.Count == 0) continue;

            var ordered = displayed.OrderBy(kv => kv.Key).ToList();
            var current = ordered[^1].Value;
            var previous = ordered.Count > 1 ? ordered[^2].Value : (decimal?)null;

            var trend = previous is null ? "Stable"
                : current > previous * 1.001m || (previous <= 0 && current > previous) ? "Rising"
                : current < previous * 0.999m || (previous <= 0 && current < previous) ? "Falling"
                : "Stable";

            var signal = SignalClassifier.Classify(type, current);
            var severity = RegimeInsight.SeverityOf(signal);

            indicators[type.ToString()] = new MacroIndicatorValueDto(
                Math.Round(current, 2),
                signal,
                trend,
                RegimeInsight.ToneOf(signal),
                severity,
                severity >= RegimeInsight.MarketMoverThreshold);
        }

        var asOf = indicators.Count > 0
            ? await _macro.GetLatestDateOverallAsync(ct) ?? DateTime.UtcNow
            : DateTime.UtcNow;

        var signals = indicators.ToDictionary(kv => kv.Key, kv => kv.Value.Signal);
        var health = RegimeInsight.ComputeMarketHealth(signals);

        return new MacroRegimeDto(
            indicators,
            health,
            RegimeInsight.Summarize(indicators),
            RegimeInsight.ComputePlaybook(health, signals),
            asOf);
    }

    public async Task<List<HistoricalMatchDto>> FindSimilarPeriodsAsync(
        int topK = 10,
        MatchingOptions? options = null,
        DateTime? minAnalogDate = null,
        CancellationToken ct = default)
    {
        var snapshots = await _snapshots.BuildAllAsync(ct);

        if (snapshots.Count < 2)
            return [];

        var matches = MacroSnapshotBuilder.FindMatches(
            snapshots, snapshots.Count - 1, topK, options, minAnalogDate);

        return matches
            .Select(m => new HistoricalMatchDto(
                m.Snapshot.Date,
                Math.Round(m.Similarity, 1),
                TopPercent: m.CandidateCount > 0
                    ? Math.Round((double)m.Rank / m.CandidateCount * 100, 1)
                    : 100,
                m.Snapshot.Values.ToDictionary(kv => kv.Key.ToString(), kv => Math.Round(kv.Value, 2))))
            .ToList();
    }
}
