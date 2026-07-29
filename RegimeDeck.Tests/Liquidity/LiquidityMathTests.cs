using RegimeDeck.Application.Liquidity;

namespace RegimeDeck.Tests.Liquidity;

// The unit normalization is the whole ballgame here: WALCL/TGA arrive in
// millions, RRP in billions, ECB in millions of euros, BoJ in units of 100
// million yen. These pin each conversion with real-world-scale sanity numbers.
public class LiquidityMathTests
{
    [Fact]
    public void FedNetLiquidity_NormalizesMillionsAndBillions()
    {
        // WALCL 6,600,000 Mil ($6.6T), TGA 800,000 Mil ($0.8T), RRP 500 Bil ($0.5T)
        var net = LiquidityMath.FedNetLiquidityBil(6_600_000m, 800_000m, 500m);
        Assert.Equal(5300m, net); // 6600 - 800 - 500
    }

    [Fact]
    public void FedBil_MillionsToBillions()
    {
        Assert.Equal(6600m, LiquidityMath.FedBil(6_600_000m));
    }

    [Fact]
    public void EcbBil_ConvertsEurosToUsdBillions()
    {
        // 6,400,000 Mil € × 1.08 USD/€ ÷ 1000 = 6912 Bil USD (~$6.9T)
        Assert.Equal(6912m, LiquidityMath.EcbBil(6_400_000m, 1.08m));
    }

    [Fact]
    public void BojBil_ConvertsHundredMillionYenToUsdBillions()
    {
        // 7,500,000 (×100 Mil ¥ = 750T ¥) ÷ 150 ¥/$ = ~$5.0T = 5000 Bil
        Assert.Equal(5000m, LiquidityMath.BojBil(7_500_000m, 150m));
    }

    [Fact]
    public void BojBil_ZeroFx_IsZeroNotDivideByZero()
    {
        Assert.Equal(0m, LiquidityMath.BojBil(7_500_000m, 0m));
    }

    [Theory]
    [InlineData(0.6, "Expanding")]
    [InlineData(0.5, "Flat")]
    [InlineData(-0.5, "Flat")]
    [InlineData(-0.6, "Contracting")]
    [InlineData(0, "Flat")]
    public void Trend_BandEdges(double changePct, string expected)
    {
        Assert.Equal(expected, LiquidityMath.Trend(changePct));
    }

    [Fact]
    public void ChangePct_ComputesFromNPointsBack()
    {
        Assert.Equal(10.0, LiquidityMath.ChangePct([100m, 105m, 110m], back: 2), 6);
    }

    [Fact]
    public void ChangePct_NotEnoughHistory_IsZero()
    {
        Assert.Equal(0.0, LiquidityMath.ChangePct([100m], back: 2));
    }

    [Fact]
    public void AsOf_ForwardFillsLatestOnOrBefore()
    {
        var series = new (DateTime, decimal)[]
        {
            (new DateTime(2026, 1, 1), 1m),
            (new DateTime(2026, 2, 1), 2m),
            (new DateTime(2026, 3, 1), 3m),
        };

        Assert.Equal(2m, LiquidityMath.AsOf(series, new DateTime(2026, 2, 15)));
        Assert.Equal(3m, LiquidityMath.AsOf(series, new DateTime(2026, 4, 1)));
        Assert.Null(LiquidityMath.AsOf(series, new DateTime(2025, 12, 31)));
    }

    [Fact]
    public void Correlation_PerfectPositiveAndNegative()
    {
        Assert.Equal(1.0, LiquidityMath.Correlation([1, 2, 3, 4], [10, 20, 30, 40]), 6);
        Assert.Equal(-1.0, LiquidityMath.Correlation([1, 2, 3, 4], [40, 30, 20, 10]), 6);
    }

    [Fact]
    public void Correlation_TooFewOrConstant_IsZero()
    {
        Assert.Equal(0.0, LiquidityMath.Correlation([1, 2], [1, 2]));
        Assert.Equal(0.0, LiquidityMath.Correlation([5, 5, 5, 5], [1, 2, 3, 4]));
    }
}
