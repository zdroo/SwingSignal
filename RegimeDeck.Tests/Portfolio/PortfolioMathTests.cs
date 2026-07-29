using RegimeDeck.Application.Portfolio;
using RegimeDeck.Domain.Enums;

namespace RegimeDeck.Tests.Portfolio;

public class PortfolioMathTests
{
    [Fact]
    public void NormalizeWeights_FromDollarValues()
    {
        Assert.Equal([0.25, 0.75], PortfolioMath.NormalizeWeights([10m, 30m]));
    }

    [Fact]
    public void NormalizeWeights_ZeroTotal_AllZero()
    {
        Assert.Equal([0.0, 0.0], PortfolioMath.NormalizeWeights([0m, 0m]));
    }

    [Theory]
    [InlineData(new[] { 1.0 }, 10000)]              // one holding
    [InlineData(new[] { 0.5, 0.5 }, 5000)]          // two equal
    [InlineData(new[] { 0.25, 0.25, 0.25, 0.25 }, 2500)] // four equal
    public void Hhi_OverPercentageWeights(double[] weights, int expected)
    {
        Assert.Equal(expected, PortfolioMath.Hhi(weights));
    }

    [Theory]
    [InlineData(10000, "Concentrated")]
    [InlineData(5000, "Concentrated")]
    [InlineData(4999, "Moderate")]
    [InlineData(2500, "Moderate")]
    [InlineData(2499, "Diversified")]
    public void ConcentrationLabel_Bands(int hhi, string label)
    {
        Assert.Equal(label, PortfolioMath.ConcentrationLabel(hhi));
    }

    [Theory]
    [InlineData("GLD", MarketType.Commodity, "Gold", false)]
    [InlineData("TLT", MarketType.Stock, "Bonds", false)]
    [InlineData("BTCUSDT", MarketType.Crypto, "Crypto", true)]
    [InlineData("SPY", MarketType.Index, "Stocks", true)]
    [InlineData("AAPL", MarketType.Stock, "Stocks", true)]
    [InlineData("CL=F", MarketType.Commodity, "Commodity", true)]
    [InlineData("EURUSD=X", MarketType.Forex, "Forex", false)]
    public void Classify_BySymbolThenMarket(string symbol, MarketType market, string cls, bool riskOn)
    {
        var (c, r) = PortfolioMath.Classify(symbol, market);
        Assert.Equal(cls, c);
        Assert.Equal(riskOn, r);
    }

    [Theory]
    [InlineData(14, "Very low")]
    [InlineData(15, "Low")]
    [InlineData(29, "Low")]
    [InlineData(30, "Modest")]
    public void Confidence_Bands(int analogs, string label)
    {
        Assert.Equal(label, PortfolioMath.Confidence(analogs));
    }

    [Fact]
    public void WeightedPercentile_AndPositiveOdds()
    {
        var samples = new[] { (-10m, 1.0), (5m, 1.0), (20m, 2.0) };
        var sorted = samples.OrderBy(s => s.Item1).Select(s => (s.Item1, s.Item2)).ToList();
        var total = 4.0;

        Assert.Equal(20m, PortfolioMath.WeightedPercentile(sorted, total, 0.75));
        Assert.Equal(75.0, PortfolioMath.WeightedPositiveOdds(sorted), 3); // 3 of 4 weight positive
    }

    [Fact]
    public void Volatility_ConstantIsZero_ShortIsNull()
    {
        Assert.Null(PortfolioMath.AnnualizedVolatilityPct([1m, 2m, 3m]));
        Assert.Equal(0.0, PortfolioMath.AnnualizedVolatilityPct(Enumerable.Repeat(100m, 30).ToList())!.Value, 6);
    }

    [Fact]
    public void LiquidityCorrelation_CoMovingRamps_StronglyPositive_ShortIsNull()
    {
        var liquidity = Enumerable.Range(1, 120).Select(i => (decimal)(1000 + i * 10)).ToList();
        var asset = Enumerable.Range(1, 120).Select(i => (decimal)(50 + i * 3)).ToList();

        Assert.True(PortfolioMath.LiquidityCorrelation(asset, liquidity) > 50);
        Assert.Null(PortfolioMath.LiquidityCorrelation([1m, 2m, 3m], [1m, 2m, 3m]));
    }
}
