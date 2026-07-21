using Microsoft.Extensions.Caching.Memory;
using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Contracts.Regime;
using RegimeDeck.Domain.Enums;

namespace RegimeDeck.Application.Regime;

public class MacroRegimeService : IMacroRegimeService
{
    // The regime is the hottest read (every landing/dashboard visit) but its
    // source data only changes when the daily FRED ingestion runs — so cache it
    // like the other computed boards. A sparse result (first ingestion still
    // running) gets a short TTL so a cold start isn't pinned for the full window.
    private const string CacheKey = "current-regime";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan EmptyCacheTtl = TimeSpan.FromSeconds(30);

    private readonly IMacroRepository _macro;
    private readonly MacroSnapshotBuilder _snapshots;
    private readonly IMemoryCache _cache;

    public MacroRegimeService(IMacroRepository macro, MacroSnapshotBuilder snapshots, IMemoryCache cache)
    {
        _macro = macro;
        _snapshots = snapshots;
        _cache = cache;
    }

    public async Task<MacroRegimeDto> GetCurrentRegimeAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out MacroRegimeDto? cached) && cached is not null)
            return cached;

        var regime = await BuildCurrentRegimeAsync(ct);

        _cache.Set(CacheKey, regime, regime.Indicators.Count > 0 ? CacheTtl : EmptyCacheTtl);
        return regime;
    }

    private async Task<MacroRegimeDto> BuildCurrentRegimeAsync(CancellationToken ct)
    {
        var indicators = new Dictionary<string, MacroIndicatorValueDto>();

        foreach (var type in MacroSnapshotBuilder.VectorIndicators)
        {
            // A YoY series needs the current month AND its year-ago baseline
            // inside the fetched window. A 14-month window (the old value)
            // left under 2 months of margin between "now" and "cutoff+12mo" —
            // any indicator whose latest release lags "now" by more than that
            // (GDP's quarterly cadence, PCE/M2's ~4-6 week publication lag)
            // silently vanished from the current regime every single month,
            // not just transiently. ~20 months gives ~8 months of margin.
            var cutoff = DateTime.UtcNow.AddDays(-600);
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
