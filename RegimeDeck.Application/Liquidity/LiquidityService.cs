using Microsoft.Extensions.Caching.Memory;
using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Contracts.Liquidity;
using RegimeDeck.Domain.Enums;

namespace RegimeDeck.Application.Liquidity;

public class LiquidityService : ILiquidityService
{
    private const string CacheKey = "liquidity-dashboard";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan EmptyCacheTtl = TimeSpan.FromSeconds(30);

    private const string BtcSymbol = "BTCUSDT";
    private const string SpySymbol = "SPY";
    private const int ThreeMonthWeeks = 13;   // ~3 months of weekly points
    private const int YoyWeeks = 52;          // 1-year lookback for growth rates
    private const int MaxOverlayWeeks = 520;  // ~10y — a common, modern window for both assets
    private const int MinOverlaySamples = 24;

    private const string Note =
        "Global liquidity is the total central-bank balance sheet (Fed + ECB + Bank of Japan) converted " +
        "to US dollars; Fed net liquidity strips the Treasury's cash account and reverse-repo drain out of " +
        "the Fed's balance sheet. Liquidity co-moves strongly with risk assets, but the lead time is " +
        "unstable — this is context, not a timing signal, and not financial advice.";

    // Every raw series the dashboard needs, all normalized to USD inside.
    private static readonly MacroIndicatorType[] Inputs =
    [
        MacroIndicatorType.FedBalanceSheet, MacroIndicatorType.TreasuryGeneralAccount,
        MacroIndicatorType.ReverseRepo, MacroIndicatorType.EcbBalanceSheet,
        MacroIndicatorType.BojBalanceSheet, MacroIndicatorType.EurUsd, MacroIndicatorType.JpyUsd,
        MacroIndicatorType.M2MoneySupply, MacroIndicatorType.DollarIndex,
    ];

    private readonly IMacroRepository _macro;
    private readonly ICandleRepository _candles;
    private readonly IMemoryCache _cache;

    public LiquidityService(IMacroRepository macro, ICandleRepository candles, IMemoryCache cache)
    {
        _macro = macro;
        _candles = candles;
        _cache = cache;
    }

    public async Task<LiquidityDashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out LiquidityDashboardDto? cached) && cached is not null)
            return cached;

        var dashboard = await BuildAsync(ct);
        _cache.Set(CacheKey, dashboard, dashboard.Series.Count > 0 ? CacheTtl : EmptyCacheTtl);
        return dashboard;
    }

    private async Task<LiquidityDashboardDto> BuildAsync(CancellationToken ct)
    {
        var points = await _macro.GetForTypesAsync(Inputs, ct);
        var byType = points
            .GroupBy(p => p.IndicatorType)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Date).Select(p => (p.Date, p.Value)).ToList());

        List<(DateTime Date, decimal Value)> Series(MacroIndicatorType t) => byType.GetValueOrDefault(t) ?? [];

        var walcl = Series(MacroIndicatorType.FedBalanceSheet);
        var tga = Series(MacroIndicatorType.TreasuryGeneralAccount);
        var rrp = Series(MacroIndicatorType.ReverseRepo);
        var ecb = Series(MacroIndicatorType.EcbBalanceSheet);
        var boj = Series(MacroIndicatorType.BojBalanceSheet);
        var eur = Series(MacroIndicatorType.EurUsd);
        var jpy = Series(MacroIndicatorType.JpyUsd);
        var m2 = Series(MacroIndicatorType.M2MoneySupply);
        var dxy = Series(MacroIndicatorType.DollarIndex);

        var required = new[] { walcl, tga, rrp, ecb, boj, eur, jpy };
        if (required.Any(s => s.Count == 0))
            return Warming();

        // Anchor the weekly grid to WALCL (the Fed release cadence), starting
        // only where every input is available so both lines are fully defined.
        var start = required.Max(s => s[0].Date);
        var spine = walcl.Where(p => p.Date >= start).Select(p => p.Date).ToList();
        if (spine.Count == 0)
            return Warming();

        var btc = await CandleSeriesAsync(BtcSymbol, ct);
        var spy = await CandleSeriesAsync(SpySymbol, ct);

        var globalBil = new List<decimal>();
        var fedNetBil = new List<decimal>();
        var m2Bil = new List<decimal>();
        var dxyLvl = new List<decimal>();
        var btcAligned = new List<decimal?>();
        var spyAligned = new List<decimal?>();
        var series = new List<LiquidityPointDto>(spine.Count);

        foreach (var date in spine)
        {
            var fed = LiquidityMath.FedBil(LiquidityMath.AsOf(walcl, date) ?? 0m);
            var ecbBil = LiquidityMath.EcbBil(LiquidityMath.AsOf(ecb, date) ?? 0m, LiquidityMath.AsOf(eur, date) ?? 0m);
            var bojBil = LiquidityMath.BojBil(LiquidityMath.AsOf(boj, date) ?? 0m, LiquidityMath.AsOf(jpy, date) ?? 0m);
            var global = fed + ecbBil + bojBil;
            var net = LiquidityMath.FedNetLiquidityBil(
                LiquidityMath.AsOf(walcl, date) ?? 0m, LiquidityMath.AsOf(tga, date) ?? 0m, LiquidityMath.AsOf(rrp, date) ?? 0m);

            var btcClose = LiquidityMath.AsOf(btc, date);
            var spyClose = LiquidityMath.AsOf(spy, date);

            globalBil.Add(global);
            fedNetBil.Add(net);
            m2Bil.Add(LiquidityMath.AsOf(m2, date) ?? 0m);
            dxyLvl.Add(LiquidityMath.AsOf(dxy, date) ?? 0m);
            btcAligned.Add(btcClose);
            spyAligned.Add(spyClose);

            series.Add(new LiquidityPointDto(date, Trillions(global), Trillions(net), btcClose, spyClose));
        }

        var lastFed = LiquidityMath.FedBil(walcl[^1].Value);
        var lastEcb = LiquidityMath.EcbBil(ecb[^1].Value, eur[^1].Value);
        var lastBoj = LiquidityMath.BojBil(boj[^1].Value, jpy[^1].Value);
        var lastGlobal = globalBil[^1];

        var components = new List<LiquidityComponentDto>
        {
            Component("Federal Reserve", lastFed, lastGlobal),
            Component("ECB", lastEcb, lastGlobal),
            Component("Bank of Japan", lastBoj, lastGlobal),
        };

        var overlays = new List<LiquidityOverlayDto>();
        AddOverlay(overlays, "BTC", globalBil, btcAligned);
        AddOverlay(overlays, "SPY", globalBil, spyAligned);

        var dashboard = new LiquidityDashboardDto(
            spine[^1],
            Reading("Global liquidity", "$T", Trillions(lastGlobal), LiquidityMath.ChangePct(globalBil, ThreeMonthWeeks), inverse: false),
            Reading("Fed net liquidity", "$T", Trillions(fedNetBil[^1]), LiquidityMath.ChangePct(fedNetBil, ThreeMonthWeeks), inverse: false),
            components,
            Reading("US M2", "$T", Trillions(m2Bil[^1]), LiquidityMath.ChangePct(m2Bil, ThreeMonthWeeks), inverse: false),
            Reading("US dollar (DXY)", "index", decimal.Round(dxyLvl[^1], 2), LiquidityMath.ChangePct(dxyLvl, ThreeMonthWeeks), inverse: true),
            series,
            overlays,
            Note);

        return dashboard;
    }

    private async Task<List<(DateTime Date, decimal Value)>> CandleSeriesAsync(string symbol, CancellationToken ct)
    {
        var candles = await _candles.GetBySymbolAsync(symbol, CandleInterval.OneDay, limit: 100_000, ct);
        return candles.OrderBy(c => c.OpenTime).Select(c => (c.OpenTime, c.Close)).ToList();
    }

    private static decimal Trillions(decimal billions) => decimal.Round(billions / 1000m, 3);

    private static LiquidityComponentDto Component(string name, decimal valueBil, decimal globalBil) =>
        new(name, Trillions(valueBil), globalBil == 0m ? 0 : Math.Round((double)(valueBil / globalBil) * 100, 1));

    private static LiquidityReadingDto Reading(string name, string unit, decimal value, double change, bool inverse)
    {
        var expanding = change > 0.5;
        var contracting = change < -0.5;
        var chg = $"{change:+0.0;-0.0}% over 3 months";

        string trend, tone, plain;
        if (!inverse)
        {
            trend = expanding ? "Expanding" : contracting ? "Contracting" : "Flat";
            tone = expanding ? "good" : contracting ? "bad" : "neutral";
            var impl = expanding ? "historically a tailwind for risk assets"
                     : contracting ? "historically a headwind for risk assets"
                     : "a neutral backdrop for risk assets";
            plain = $"{name} is {trend.ToLowerInvariant()} ({chg}) — {impl}.";
        }
        else
        {
            trend = expanding ? "Rising" : contracting ? "Falling" : "Flat";
            tone = expanding ? "bad" : contracting ? "good" : "neutral";
            var impl = expanding ? "a stronger dollar drains global liquidity"
                     : contracting ? "a weaker dollar adds global liquidity"
                     : "little dollar pressure either way";
            plain = $"The dollar is {trend.ToLowerInvariant()} ({chg}) — {impl}.";
        }

        return new LiquidityReadingDto(name, value, unit, Math.Round(change, 1), trend, tone, plain);
    }

    // Correlate YoY GROWTH of liquidity with YoY RETURN of the asset — not raw
    // levels. Two long-rising series correlate spuriously on levels (and can even
    // go negative in a QT/strong-dollar window); rate-of-change is the honest,
    // standard measure of whether they actually move together.
    private static void AddOverlay(List<LiquidityOverlayDto> into, string symbol,
        List<decimal> global, List<decimal?> asset)
    {
        var xs = new List<double>();
        var ys = new List<double>();
        var startIdx = Math.Max(YoyWeeks, global.Count - MaxOverlayWeeks);
        for (var i = startIdx; i < global.Count; i++)
        {
            if (asset[i] is not decimal cur || asset[i - YoyWeeks] is not decimal prev) continue;
            if (prev == 0m || global[i - YoyWeeks] == 0m) continue;
            xs.Add((double)(global[i] / global[i - YoyWeeks] - 1m));
            ys.Add((double)(cur / prev - 1m));
        }

        if (xs.Count < MinOverlaySamples) return;
        var months = (int)Math.Round(xs.Count / 4.345);
        into.Add(new LiquidityOverlayDto(symbol, (int)Math.Round(LiquidityMath.Correlation(xs, ys) * 100), months));
    }

    private static LiquidityDashboardDto Warming()
    {
        var neutral = new LiquidityReadingDto("", 0m, "$T", 0, "Flat", "neutral", "");
        return new LiquidityDashboardDto(
            DateTime.UtcNow, neutral, neutral, [], neutral, neutral, [], [],
            "Liquidity data is still loading — check back once the first ingestion cycle completes.");
    }
}
