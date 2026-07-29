using Microsoft.Extensions.Caching.Memory;
using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Application.Liquidity;
using RegimeDeck.Domain.Entities;
using RegimeDeck.Domain.Enums;

namespace RegimeDeck.Tests.Liquidity;

// End-to-end over fakes: proves the raw source series (different units, weekly
// grid, candle overlay) come out as sane USD-trillion magnitudes, correct
// shares, trends and correlations.
public class LiquidityServiceTests
{
    private static readonly DateTime Base = new(2024, 1, 3);
    private const int Weeks = 120; // > 52 so the YoY-growth correlation has samples

    private static readonly DateTime[] Dates =
        Enumerable.Range(0, Weeks).Select(i => Base.AddDays(7 * i)).ToArray();

    private static LiquidityService Service(FakeMacro macro, FakeCandles candles) =>
        new(macro, candles, new MemoryCache(new MemoryCacheOptions()));

    // A ramp from `start` to `end` across the 20 weekly dates.
    private static decimal[] Ramp(decimal start, decimal end) =>
        Enumerable.Range(0, Weeks).Select(i => start + (end - start) * i / (Weeks - 1)).ToArray();

    private static decimal[] Flat(decimal v) => Enumerable.Repeat(v, Weeks).ToArray();

    private static FakeMacro FullMacro()
    {
        var macro = new FakeMacro();
        macro.Seed(MacroIndicatorType.FedBalanceSheet, Ramp(5_400_000m, 6_600_000m)); // Mil $
        macro.Seed(MacroIndicatorType.TreasuryGeneralAccount, Flat(800_000m));        // Mil $
        macro.Seed(MacroIndicatorType.ReverseRepo, Flat(500m));                       // Bil $
        macro.Seed(MacroIndicatorType.EcbBalanceSheet, Flat(6_400_000m));             // Mil €
        macro.Seed(MacroIndicatorType.BojBalanceSheet, Flat(7_500_000m));             // 100 Mil ¥
        macro.Seed(MacroIndicatorType.EurUsd, Flat(1.08m));
        macro.Seed(MacroIndicatorType.JpyUsd, Flat(150m));
        macro.Seed(MacroIndicatorType.M2MoneySupply, Ramp(19_000m, 21_000m));         // Bil $
        macro.Seed(MacroIndicatorType.DollarIndex, Ramp(108m, 100m));                 // index, falling
        return macro;
    }

    private static FakeCandles FullCandles()
    {
        var candles = new FakeCandles();
        candles.Seed("BTCUSDT", 10_000m, 50m);
        candles.Seed("SPY", 400m, 0.5m);
        return candles;
    }

    [Fact]
    public async Task Global_And_FedNet_HaveSaneUsdTrillionMagnitudes()
    {
        var d = await Service(FullMacro(), FullCandles()).GetDashboardAsync();

        // Fed 6.6 + ECB 6.4×1.08=6.912 + BoJ 7.5M×0.1/150=5.0 → 18.512 $T
        Assert.Equal(18.512m, d.GlobalLiquidity.Value);
        // 6.6 − 0.8 − 0.5 → 5.3 $T
        Assert.Equal(5.3m, d.FedNetLiquidity.Value);
        Assert.Equal("$T", d.GlobalLiquidity.Unit);
    }

    [Fact]
    public async Task Components_SumToGlobal_WithShares()
    {
        var d = await Service(FullMacro(), FullCandles()).GetDashboardAsync();

        Assert.Equal(["Federal Reserve", "ECB", "Bank of Japan"], d.Components.Select(c => c.Name));
        Assert.Equal(6.6m, d.Components[0].ValueUsdTrillions);
        Assert.InRange(d.Components.Sum(c => c.SharePct), 99.0, 101.0);
    }

    [Fact]
    public async Task ExpandingLiquidity_ReadsAsGoodTailwind()
    {
        var d = await Service(FullMacro(), FullCandles()).GetDashboardAsync();

        Assert.Equal("Expanding", d.GlobalLiquidity.Trend);
        Assert.Equal("good", d.GlobalLiquidity.Tone);
        Assert.Contains("tailwind", d.GlobalLiquidity.Plain);
    }

    [Fact]
    public async Task FallingDollar_ReadsAsGoodForRisk()
    {
        var d = await Service(FullMacro(), FullCandles()).GetDashboardAsync();

        Assert.Equal("index", d.Dollar.Unit);
        Assert.Equal("Falling", d.Dollar.Trend);
        Assert.Equal("good", d.Dollar.Tone);
    }

    [Fact]
    public async Task Series_SpansTheGrid_AndCarriesOverlayCloses()
    {
        var d = await Service(FullMacro(), FullCandles()).GetDashboardAsync();

        Assert.Equal(Weeks, d.Series.Count);
        Assert.Equal(18.512m, d.Series[^1].GlobalLiquidity);
        Assert.NotNull(d.Series[^1].Btc);
        Assert.NotNull(d.Series[^1].Spy);
    }

    [Fact]
    public async Task Overlays_PickUpTheCoMovement()
    {
        var d = await Service(FullMacro(), FullCandles()).GetDashboardAsync();

        var btc = d.Overlays.Single(o => o.Symbol == "BTC");
        Assert.True(btc.CorrelationPct > 50); // both rise together
        Assert.Contains(d.Overlays, o => o.Symbol == "SPY");
    }

    [Fact]
    public async Task MissingRequiredSeries_ReturnsWarmingState()
    {
        var macro = FullMacro();
        macro.Clear(MacroIndicatorType.EcbBalanceSheet); // one required leg absent

        var d = await Service(macro, FullCandles()).GetDashboardAsync();

        Assert.Empty(d.Series);
        Assert.Empty(d.Components);
        Assert.Contains("loading", d.Note);
    }

    private sealed class FakeMacro : IMacroRepository
    {
        private readonly Dictionary<MacroIndicatorType, decimal[]> _series = [];
        public void Seed(MacroIndicatorType type, decimal[] values) => _series[type] = values;
        public void Clear(MacroIndicatorType type) => _series.Remove(type);

        public Task<List<MacroDataPoint>> GetForTypesAsync(
            IReadOnlyCollection<MacroIndicatorType> types, CancellationToken ct = default)
        {
            var points = new List<MacroDataPoint>();
            foreach (var type in types)
            {
                if (!_series.TryGetValue(type, out var values)) continue;
                for (var i = 0; i < values.Length; i++)
                    points.Add(new MacroDataPoint { IndicatorType = type, Date = Dates[i], Value = values[i], Source = "test" });
            }
            return Task.FromResult(points);
        }

        public Task<List<MacroDataPoint>> GetByTypeAsync(MacroIndicatorType type, int limit = 100, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<MacroDataPoint>> GetSinceAsync(MacroIndicatorType type, DateTime from, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<MacroDataPoint?> GetLatestAsync(MacroIndicatorType type, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DateTime?> GetLatestDateAsync(MacroIndicatorType type, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DateTime?> GetLatestDateOverallAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task BulkInsertAsync(IEnumerable<MacroDataPoint> points, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<MacroDataPoint>> GetLatestSnapshotAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeCandles : ICandleRepository
    {
        private readonly Dictionary<string, List<Candle>> _bySymbol = [];

        // Daily candles across the whole grid, close = start + step per day.
        public void Seed(string symbol, decimal start, decimal step)
        {
            var list = new List<Candle>();
            var days = (Dates[^1] - Dates[0]).Days;
            for (var i = 0; i <= days; i++)
                list.Add(new Candle { OpenTime = Dates[0].AddDays(i), Close = start + step * i, Interval = CandleInterval.OneDay });
            _bySymbol[symbol] = list;
        }

        public Task<List<Candle>> GetBySymbolAsync(string symbol, CandleInterval interval, int limit = 500, CancellationToken ct = default) =>
            Task.FromResult(_bySymbol.GetValueOrDefault(symbol) ?? []);

        public Task<List<Candle>> GetDailyHistoryAsync(Guid assetId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DateTime?> GetLatestOpenTimeAsync(Guid assetId, CandleInterval interval, CancellationToken ct = default) => throw new NotSupportedException();
        public Task BulkInsertAsync(IEnumerable<Candle> candles, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
