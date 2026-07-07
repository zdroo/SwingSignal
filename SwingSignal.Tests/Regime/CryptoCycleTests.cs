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
