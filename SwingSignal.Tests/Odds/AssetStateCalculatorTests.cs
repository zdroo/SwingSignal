using SwingSignal.Application.Odds;
using SwingSignal.Domain.Entities;
using SwingSignal.Domain.Enums;

namespace SwingSignal.Tests.Odds;

public class AssetStateCalculatorTests
{
    private static List<Candle> Candles(params decimal[] closes) =>
        closes.Select((c, i) => new Candle
        {
            OpenTime = new DateTime(2020, 1, 1).AddDays(i),
            Open = c, High = c, Low = c, Close = c,
            Volume = 1,
            Interval = CandleInterval.OneDay
        }).ToList();

    [Fact]
    public void ComputeAt_NeedsAtLeast200PriorCandles()
    {
        var candles = Candles(Enumerable.Repeat(100m, 250).ToArray());

        Assert.Null(AssetStateCalculator.ComputeAt(candles, index: 199));
        Assert.NotNull(AssetStateCalculator.ComputeAt(candles, index: 200));
    }

    [Fact]
    public void ComputeAt_FlatSeries_IsExactlyNeutral()
    {
        var candles = Candles(Enumerable.Repeat(100m, 250).ToArray());

        var state = AssetStateCalculator.ComputeAt(candles, index: 240)!;

        Assert.Equal(0.0, state.Ma200Pos);     // price == its 200-day average
        Assert.Equal(0.0, state.High52Dist);   // price == its 52-week high
        Assert.Equal(100.0, state.Rsi14);      // zero losses convention
    }

    [Fact]
    public void ComputeAt_StrictlyFallingLast14_RsiIsZero()
    {
        // Flat at 500, then the last 15 candles fall by 1 each day
        var closes = Enumerable.Repeat(500m, 235).Concat(
            Enumerable.Range(1, 15).Select(i => 500m - i)).ToArray();
        var candles = Candles(closes);

        var state = AssetStateCalculator.ComputeAt(candles, index: 249)!;

        Assert.Equal(0.0, state.Rsi14); // only losses in the window
    }

    [Fact]
    public void Distance_IsRmsOfZScoredFeatureDiffs()
    {
        var a = new AssetState(0, 0, 50);
        var b = new AssetState(10, -10, 60);
        var stds = new AssetStateStds(10, 10, 10);

        // diffs in std units: 1, 1, 1 => sqrt(3/3) = 1
        Assert.Equal(1.0, AssetStateCalculator.Distance(a, b, stds), precision: 12);
    }

    [Fact]
    public void StateFactor_GaussianOfDistanceOverBandwidth()
    {
        var a = new AssetState(0, 0, 50);
        var b = new AssetState(10, -10, 60);
        var stds = new AssetStateStds(10, 10, 10);

        Assert.Equal(Math.Exp(-1.0), AssetStateCalculator.StateFactor(a, b, stds, bandwidth: 1.0), precision: 12);
        Assert.Equal(Math.Exp(-0.25), AssetStateCalculator.StateFactor(a, b, stds, bandwidth: 2.0), precision: 12);
    }

    [Fact]
    public void ComputeStds_FewerThan24States_IsUnusable()
    {
        var states = Enumerable.Range(0, 23).Select(i => new AssetState(i, -i, 50));

        var stds = AssetStateCalculator.ComputeStds(states);

        Assert.False(stds.IsUsable);
        Assert.Equal(0.0, stds.Ma200Pos);
    }
}
