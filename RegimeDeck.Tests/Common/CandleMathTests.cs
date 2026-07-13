using RegimeDeck.Application.Common;
using RegimeDeck.Domain.Entities;
using RegimeDeck.Domain.Enums;

namespace RegimeDeck.Tests.Common;

public class CandleMathTests
{
    private static List<Candle> DailyCandles(int count, Func<int, decimal> close, DateTime? start = null)
    {
        var s = start ?? new DateTime(2020, 1, 1);
        return Enumerable.Range(0, count)
            .Select(i => new Candle
            {
                OpenTime = s.AddDays(i),
                Open = close(i),
                High = close(i),
                Low = close(i),
                Close = close(i),
                Volume = 1,
                Interval = CandleInterval.OneDay
            })
            .ToList();
    }

    [Theory]
    [InlineData(7, 2)]    // 7/3 = 2, at the floor
    [InlineData(12, 4)]
    [InlineData(21, 7)]
    [InlineData(90, 7)]   // clamped to the ceiling
    public void ExitWindow_ClampsToExpectedRange(int horizonDays, int expected)
    {
        Assert.Equal(expected, CandleMath.ExitWindow(horizonDays));
    }

    [Theory]
    [InlineData(7, 5)]
    [InlineData(30, 21)]
    [InlineData(90, 64)]
    [InlineData(1, 1)]    // floors at 1
    public void HorizonCandles_ConvertsCalendarToTradingDays(int horizonDays, int expected)
    {
        Assert.Equal(expected, CandleMath.HorizonCandles(horizonDays));
    }

    [Fact]
    public void PercentReturn_ComputesExactRoundedPercentage()
    {
        Assert.Equal(10.00m, CandleMath.PercentReturn(100m, 110m));
        Assert.Equal(-4.50m, CandleMath.PercentReturn(100m, 95.5m));
        Assert.Equal(89.11m, CandleMath.PercentReturn(101m, 191m)); // 89.108910...
    }

    [Fact]
    public void FindNearestIndex_ReturnsClosestCandleWithinWindow()
    {
        // Candles on Jan 1, 3, 5, 7
        var candles = DailyCandles(4, _ => 100m).Select((c, i) =>
        {
            c.OpenTime = new DateTime(2020, 1, 1).AddDays(i * 2);
            return c;
        }).ToList();

        // Jan 4 is equidistant from Jan 3 and Jan 5 — earlier candle wins (strict <)
        Assert.Equal(1, CandleMath.FindNearestIndex(candles, new DateTime(2020, 1, 4), windowDays: 7));
        // Exact hit
        Assert.Equal(2, CandleMath.FindNearestIndex(candles, new DateTime(2020, 1, 5), windowDays: 7));
        // Before the series
        Assert.Equal(0, CandleMath.FindNearestIndex(candles, new DateTime(2019, 12, 30), windowDays: 7));
        // Outside the window
        Assert.Equal(-1, CandleMath.FindNearestIndex(candles, new DateTime(2020, 1, 20), windowDays: 7));
        // Empty series
        Assert.Equal(-1, CandleMath.FindNearestIndex([], new DateTime(2020, 1, 1), windowDays: 7));
    }

    [Fact]
    public void SampleOutcomes_StridesMonthlyAndComparesEntryToExit()
    {
        // 64 rising candles, 30-day horizon => 21 trading candles; entries at 0, 21, 42
        var candles = DailyCandles(64, i => i + 1);

        var samples = CandleMath.SampleOutcomes(candles, horizonDays: 30);

        Assert.Equal(3, samples.Count);
        Assert.Equal(candles[21].OpenTime, samples[0].ExitDate);
        Assert.Equal(candles[42].OpenTime, samples[1].ExitDate);
        Assert.Equal(candles[63].OpenTime, samples[2].ExitDate);
        Assert.All(samples, s => Assert.True(s.Positive));
    }

    [Fact]
    public void ComputeBaseRate_ReturnsNullBelowMinimumSamples()
    {
        var candles = DailyCandles(64, i => i + 1); // only 3 samples at 30d
        Assert.Null(CandleMath.ComputeBaseRate(candles, horizonDays: 30));
    }

    [Fact]
    public void ComputeBaseRate_AllRising_Returns100()
    {
        var candles = DailyCandles(600, i => i + 1); // 26 samples at 90d, all positive
        Assert.Equal(100.0, CandleMath.ComputeBaseRate(candles, horizonDays: 90));
    }

    [Fact]
    public void ComputeBaseRate_AllFalling_ReturnsZero()
    {
        var candles = DailyCandles(600, i => 1000m - i);
        Assert.Equal(0.0, CandleMath.ComputeBaseRate(candles, horizonDays: 90));
    }

    [Fact]
    public void ComputeBaseRate_TrailingCutoff_UsesOnlyRecentWindows()
    {
        // 1200 days: first 600 falling, last 600 rising. All-history mixes both;
        // a cutoff inside the rising half sees only positive exits.
        var candles = DailyCandles(1200, i => i < 600 ? 2000m - i : 1000m + i);
        var cutoff = new DateTime(2020, 1, 1).AddDays(700);

        var allHistory = CandleMath.ComputeBaseRate(candles, horizonDays: 90);
        var trailing = CandleMath.ComputeBaseRate(candles, horizonDays: 90, since: cutoff);

        Assert.NotNull(allHistory);
        Assert.True(allHistory < 100.0);
        Assert.Equal(100.0, trailing);
    }

    [Fact]
    public void ComputeBaseRate_TrailingCutoff_TooFewRecentSamples_ReturnsNull()
    {
        var candles = DailyCandles(1200, i => i + 1);
        // Cutoff so late that fewer than 24 windows exit after it
        var cutoff = new DateTime(2020, 1, 1).AddDays(1150);

        Assert.Null(CandleMath.ComputeBaseRate(candles, horizonDays: 90, since: cutoff));
    }
}
