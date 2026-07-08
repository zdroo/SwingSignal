using SwingSignal.Application.Regime;
using SwingSignal.Domain.Entities;
using SwingSignal.Domain.Enums;

namespace SwingSignal.Tests.Regime;

public class CryptoCycleTests
{
    [Fact]
    public void MonthsSinceHalving_ExactMonthCounts()
    {
        // 2024-04-19 halving; ~14 months later
        Assert.Equal(14, CryptoCycle.MonthsSinceHalving(new DateTime(2025, 6, 19)));
        // Day after the halving
        Assert.Equal(0, CryptoCycle.MonthsSinceHalving(new DateTime(2024, 4, 20)));
        // Before the first halving ever
        Assert.Null(CryptoCycle.MonthsSinceHalving(new DateTime(2012, 1, 1)));
    }

    [Theory]
    [InlineData(2024, 6, 1, "quiet accumulation")]      // ~1 month after 2024 halving
    [InlineData(2025, 5, 1, "markup window")]           // ~12 months after
    [InlineData(2026, 7, 1, "late-cycle territory")]    // ~26 months after
    [InlineData(2027, 6, 1, "pre-halving consolidation")] // ~38 months after
    public void HalvingBullet_MentionsTheRightPhase(int year, int month, int day, string phaseFragment)
    {
        var bullet = CryptoCycle.HalvingBullet(new DateTime(year, month, day));

        Assert.NotNull(bullet);
        Assert.Contains(phaseFragment, bullet);
    }

    private static List<Candle> Candles(int count, Func<int, decimal> close) =>
        Enumerable.Range(0, count).Select(i => new Candle
        {
            OpenTime = new DateTime(2020, 1, 1).AddDays(i),
            Open = close(i), High = close(i), Low = close(i), Close = close(i),
            Volume = 1,
            Interval = CandleInterval.OneDay
        }).ToList();

    [Fact]
    public void MayerMultiple_FlatSeries_IsExactlyOne()
    {
        Assert.Equal(1.00m, CryptoCycle.MayerMultiple(Candles(250, _ => 100m)));
    }

    [Fact]
    public void MayerMultiple_LastPriceTripleTheAverage_IsExactlyThree()
    {
        // 200 candles at 100, then the very last at 300 => sma includes the 300:
        // build 250 flat + last replaced. sma of last 200 = (199*100 + 300)/200 = 101
        var candles = Candles(250, _ => 100m);
        candles[^1].Close = 300m;

        // 300 / 101 = 2.9703 -> 2.97
        Assert.Equal(2.97m, CryptoCycle.MayerMultiple(candles));
    }

    [Fact]
    public void MayerMultiple_TooFewCandles_ReturnsNull()
    {
        Assert.Null(CryptoCycle.MayerMultiple(Candles(199, _ => 100m)));
    }

    // ── CycleFactor ──────────────────────────────────────────────────────

    [Fact]
    public void CycleFactor_SamePhaseSameMayer_IsExactlyOne()
    {
        // Both dates 14 months past their respective halvings, identical Mayer
        var factor = CryptoCycle.CycleFactor(
            now: new DateTime(2025, 6, 19), mayerNow: 1.2m,
            analog: new DateTime(2021, 7, 11), mayerAnalog: 1.2m, // 14m past 2020-05-11
            bandwidth: 1.0);

        Assert.Equal(1.0, factor, precision: 12);
    }

    [Fact]
    public void CycleFactor_OppositePhase_ExactGaussian()
    {
        // 2024-10-19 = 6 months post-halving; 2022-11-11 = 30 months post-halving.
        // Phase fractions 6/48 and 30/48 => diff 0.5, circular min 0.5, scaled x2 = 1.0.
        // Same Mayer => factor = exp(-1/h^2)
        var factor = CryptoCycle.CycleFactor(
            new DateTime(2024, 10, 19), 1.0m,
            new DateTime(2022, 11, 11), 1.0m,
            bandwidth: 1.0);

        Assert.Equal(Math.Exp(-1.0), factor, precision: 12);
    }

    [Fact]
    public void CycleFactor_MayerGap_ExactLogDistance()
    {
        // Same phase; Mayer 2.0 vs 1.0 => dMayer = ln2 => exp(-(ln2)^2 / h^2)
        var factor = CryptoCycle.CycleFactor(
            new DateTime(2025, 6, 19), 2.0m,
            new DateTime(2021, 7, 11), 1.0m,
            bandwidth: 1.0);

        Assert.Equal(Math.Exp(-Math.Pow(Math.Log(2), 2)), factor, precision: 12);
    }

    [Fact]
    public void CycleFactor_MissingMayer_PenalizesPhaseOnly()
    {
        // Analog Mayer unknown: only the phase difference counts
        var withMissing = CryptoCycle.CycleFactor(
            new DateTime(2024, 10, 19), 1.5m,
            new DateTime(2022, 11, 11), null,
            bandwidth: 1.0);

        Assert.Equal(Math.Exp(-1.0), withMissing, precision: 12);
    }

    [Fact]
    public void CycleFactor_SmallerBandwidth_PunishesHarder()
    {
        var wide = CryptoCycle.CycleFactor(
            new DateTime(2024, 10, 19), 1.0m, new DateTime(2022, 11, 11), 1.0m, bandwidth: 1.5);
        var strict = CryptoCycle.CycleFactor(
            new DateTime(2024, 10, 19), 1.0m, new DateTime(2022, 11, 11), 1.0m, bandwidth: 0.5);

        Assert.True(strict < wide);
        Assert.Equal(Math.Exp(-4.0), strict, precision: 12);  // 1 / 0.25
    }

    [Fact]
    public void MayerMultipleAt_HistoricalIndex_UnroundedValue()
    {
        // Index 249 of a flat-100 series with the last close at 300: same math as
        // MayerMultiple but unrounded — 300/101
        var candles = Candles(250, _ => 100m);
        candles[^1].Close = 300m;

        Assert.Equal(300m / 101m, CryptoCycle.MayerMultipleAt(candles, 249));
        Assert.Null(CryptoCycle.MayerMultipleAt(candles, 198)); // not enough history
        Assert.Null(CryptoCycle.MayerMultipleAt(candles, 250)); // out of range
    }

    [Fact]
    public void MayerBullet_OverheatedZone_SaysSo()
    {
        var candles = Candles(250, _ => 100m);
        candles[^1].Close = 300m; // 2.97x > 2.4 threshold

        var bullet = CryptoCycle.MayerBullet(candles);

        Assert.NotNull(bullet);
        Assert.Contains("2.97x", bullet);
        Assert.Contains("overheated", bullet);
    }
}
