using RegimeDeck.Application.Sectors;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Tests.Sectors;

// Relative strength = asset return minus benchmark return over the window,
// in percentage points. Positive = the asset is leading ("money rotating in").
public class RelativeStrengthTests
{
    private static readonly DateTime Today = new(2026, 7, 13, 0, 0, 0, DateTimeKind.Utc);

    private static List<Candle> Candles(params (int DaysAgo, decimal Close)[] points) =>
        points
            .Select(p => new Candle { OpenTime = Today.AddDays(-p.DaysAgo), Close = p.Close })
            .OrderBy(c => c.OpenTime)
            .ToList();

    [Fact]
    public void Outperformance_IsPositive()
    {
        var asset = Candles((90, 100m), (0, 110m));      // +10%
        var benchmark = Candles((90, 100m), (0, 105m));  // +5%

        Assert.Equal(5.0, RelativeStrength.Compute(asset, benchmark, 90));
    }

    [Fact]
    public void Underperformance_IsNegative()
    {
        var asset = Candles((90, 100m), (0, 102m));      // +2%
        var benchmark = Candles((90, 100m), (0, 108m));  // +8%

        Assert.Equal(-6.0, RelativeStrength.Compute(asset, benchmark, 90));
    }

    [Fact]
    public void EqualPerformance_IsZero()
    {
        var asset = Candles((90, 50m), (0, 55m));        // +10%
        var benchmark = Candles((90, 200m), (0, 220m));  // +10%

        Assert.Equal(0.0, RelativeStrength.Compute(asset, benchmark, 90));
    }

    [Fact]
    public void RoundsToOneDecimal()
    {
        var asset = Candles((90, 100m), (0, 103.33m));      // +3.33%
        var benchmark = Candles((90, 100m), (0, 100m));     // 0%

        Assert.Equal(3.3, RelativeStrength.Compute(asset, benchmark, 90));
    }

    [Fact]
    public void InsufficientHistory_IsNull()
    {
        var single = Candles((0, 100m));
        var benchmark = Candles((90, 100m), (0, 105m));

        Assert.Null(RelativeStrength.Compute(single, benchmark, 90));
        Assert.Null(RelativeStrength.Compute(benchmark, single, 90));
    }
}
