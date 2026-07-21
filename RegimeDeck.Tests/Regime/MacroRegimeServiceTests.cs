using Microsoft.Extensions.Caching.Memory;
using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Application.Regime;
using RegimeDeck.Domain.Entities;
using RegimeDeck.Domain.Enums;

namespace RegimeDeck.Tests.Regime;

// Regression test: GDP/M2/CorePCE (all YoY-transformed, all published with a
// real-world lag behind "today") were silently vanishing from the current
// regime snapshot every month, not transiently — the fetch window left too
// little margin for a YoY pairing to succeed once an indicator's latest
// release was more than ~2 months stale. Proven with a fake repo so it
// doesn't depend on live data or wall-clock timing at test-run time.
public class MacroRegimeServiceTests
{
    private static MacroRegimeService Service(FakeMacroRepository repo) =>
        new(repo, new MacroSnapshotBuilder(repo, new MemoryCache(new MemoryCacheOptions())));

    // Two years of monthly points for one indicator, most recent point
    // `monthsStale` months behind "now" — simulating a real publication lag.
    private static List<MacroDataPoint> MonthlySeries(MacroIndicatorType type, int monthsStale, decimal start = 100m)
    {
        var latestMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-monthsStale);
        var points = new List<MacroDataPoint>();
        for (var i = 24; i >= 0; i--)
            points.Add(new MacroDataPoint
            {
                IndicatorType = type,
                Date = latestMonth.AddMonths(-i),
                Value = start + i, // any monotonic series is fine — only presence/timing matters
                Source = "test",
            });
        return points;
    }

    [Fact]
    public async Task YoYIndicator_StaleByTypicalPublicationLag_StillAppears()
    {
        // GDP-like: quarterly/monthly release running ~4 months behind today
        // (within the old 14-month window's ~2-month margin, this vanished)
        var repo = new FakeMacroRepository();
        repo.Seed(MacroIndicatorType.GDP, MonthlySeries(MacroIndicatorType.GDP, monthsStale: 4));

        var result = await Service(repo).GetCurrentRegimeAsync();

        Assert.True(result.Indicators.ContainsKey(nameof(MacroIndicatorType.GDP)));
    }

    [Fact]
    public async Task YoYIndicator_StaleBeyondTheWidenedWindow_StillAbsent()
    {
        // Sanity check the fix isn't "load everything, ignore the window" —
        // something stale enough (15 months) should still correctly drop out.
        var repo = new FakeMacroRepository();
        repo.Seed(MacroIndicatorType.GDP, MonthlySeries(MacroIndicatorType.GDP, monthsStale: 15));

        var result = await Service(repo).GetCurrentRegimeAsync();

        Assert.False(result.Indicators.ContainsKey(nameof(MacroIndicatorType.GDP)));
    }

    private sealed class FakeMacroRepository : IMacroRepository
    {
        private readonly Dictionary<MacroIndicatorType, List<MacroDataPoint>> _byType = [];

        public void Seed(MacroIndicatorType type, List<MacroDataPoint> points) => _byType[type] = points;

        public Task<List<MacroDataPoint>> GetSinceAsync(MacroIndicatorType type, DateTime from, CancellationToken ct = default) =>
            Task.FromResult(_byType.TryGetValue(type, out var points)
                ? points.Where(p => p.Date >= from).OrderBy(p => p.Date).ToList()
                : []);

        public Task<DateTime?> GetLatestDateOverallAsync(CancellationToken ct = default) =>
            Task.FromResult<DateTime?>(_byType.Values.SelectMany(p => p).Select(p => p.Date).DefaultIfEmpty().Max());

        public Task<List<MacroDataPoint>> GetByTypeAsync(MacroIndicatorType type, int limit = 100, CancellationToken ct = default) =>
            throw new NotSupportedException("Not used by GetCurrentRegimeAsync");
        public Task<List<MacroDataPoint>> GetForTypesAsync(IReadOnlyCollection<MacroIndicatorType> types, CancellationToken ct = default) =>
            throw new NotSupportedException("Not used by GetCurrentRegimeAsync");
        public Task<MacroDataPoint?> GetLatestAsync(MacroIndicatorType type, CancellationToken ct = default) =>
            throw new NotSupportedException("Not used by GetCurrentRegimeAsync");
        public Task<DateTime?> GetLatestDateAsync(MacroIndicatorType type, CancellationToken ct = default) =>
            throw new NotSupportedException("Not used by GetCurrentRegimeAsync");
        public Task BulkInsertAsync(IEnumerable<MacroDataPoint> points, CancellationToken ct = default) =>
            throw new NotSupportedException("Not used by GetCurrentRegimeAsync");
        public Task<List<MacroDataPoint>> GetLatestSnapshotAsync(CancellationToken ct = default) =>
            throw new NotSupportedException("Not used by GetCurrentRegimeAsync");
    }
}
