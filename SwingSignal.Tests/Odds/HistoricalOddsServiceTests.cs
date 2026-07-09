using Moq;
using SwingSignal.Application.Abstractions.Persistence;
using SwingSignal.Application.Common;
using SwingSignal.Application.Odds;
using SwingSignal.Application.Regime;
using SwingSignal.Contracts.Regime;
using SwingSignal.Domain.Entities;
using SwingSignal.Domain.Enums;

namespace SwingSignal.Tests.Odds;

public class HistoricalOddsServiceTests
{
    private static readonly Guid AssetId = Guid.NewGuid();
    private static readonly DateTime Start = new(2020, 1, 1);

    private static Asset SpyAsset() => new()
    {
        Id = AssetId,
        Symbol = "SPY",
        Name = "S&P 500 ETF",
        MarketType = MarketType.Index,
        IsActive = true,
    };

    /// 600 daily candles, close = index + 1 (strictly rising)
    private static List<Candle> RisingCandles() =>
        Enumerable.Range(0, 600).Select(i => new Candle
        {
            AssetId = AssetId,
            OpenTime = Start.AddDays(i),
            Open = i + 1, High = i + 1, Low = i + 1, Close = i + 1,
            Volume = 1,
            Interval = CandleInterval.OneDay
        }).ToList();

    private static HistoricalMatchDto Match(DateTime date, double similarity) =>
        new(date, similarity, TopPercent: 1, IndicatorValues: []);

    private static HistoricalOddsService BuildService(
        List<Candle> candles,
        List<HistoricalMatchDto> matches,
        out Mock<IAssetRepository> assets)
    {
        assets = new Mock<IAssetRepository>(MockBehavior.Strict);
        assets.Setup(a => a.GetBySymbolAsync("SPY", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SpyAsset());

        var candleRepo = new Mock<ICandleRepository>(MockBehavior.Strict);
        candleRepo.Setup(c => c.GetDailyHistoryAsync(AssetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(candles);

        var regime = new Mock<IMacroRegimeService>(MockBehavior.Strict);
        // Non-crypto asset: full-dimension production profile, no analog floor
        regime.Setup(r => r.FindSimilarPeriodsAsync(
                MatchingOptions.AnalogCount, MatchingOptions.Production, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(matches);

        var explainer = new Mock<IAssetExplainerService>();
        explainer.Setup(e => e.GenerateAsync("SPY", MarketType.Index, matches, candles, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["macro bullet"]);

        return new HistoricalOddsService(assets.Object, candleRepo.Object, regime.Object, explainer.Object);
    }

    [Fact]
    public async Task GetOddsForDaysAsync_RisingSeries_ExactNumbers()
    {
        var candles = RisingCandles();
        // Two analogs with equal similarity => equal kernel weights.
        // Entries land exactly on candles 100 and 200; exits 90 days later on 190 and 290.
        var matches = new List<HistoricalMatchDto>
        {
            Match(Start.AddDays(100), 50.0),
            Match(Start.AddDays(200), 50.0),
        };
        var service = BuildService(candles, matches, out _);

        var result = await service.GetOddsForDaysAsync("SPY", days: 90);

        Assert.Equal("SPY", result.Symbol);
        Assert.Equal("S&P 500 ETF", result.Name);
        Assert.Equal(90, result.Days);
        Assert.Equal(2, result.MatchesUsed);
        Assert.Equal(600m, result.CurrentPrice);

        var odds = result.Odds;
        Assert.Equal(2, odds.TotalCases);
        Assert.Equal(2, odds.PositiveCases);

        // Rising series: base rate = 100%, raw analog odds = 100% => shrunk stays 100
        Assert.Equal(100.0, odds.PositiveOdds);
        Assert.Equal(100.0, odds.BaseRate);
        Assert.Equal(0.0, odds.Edge);

        // Returns: (191-101)/101 = 89.11%, (291-201)/201 = 44.78%
        Assert.Equal(89.11m, odds.BestCase);
        Assert.Equal(44.78m, odds.WorstCase);
        Assert.Equal(66.94m, odds.AverageReturn); // (89.11+44.78)/2 = 66.945, banker's rounding

        // Equal weights: P25 and P50 hit the first sorted return, P75 the second
        Assert.Equal(44.78m, odds.MedianReturn);
        Assert.Equal(600m * 1.4478m, odds.PriceTargetLow);
        Assert.Equal(600m * 1.4478m, odds.PriceTargetMid);
        Assert.Equal(600m * 1.8911m, odds.PriceTargetHigh);
    }

    [Fact]
    public async Task GetOddsAsync_CryptoAsset_UsesCryptoProfileAndHistoryFloor()
    {
        var btcId = Guid.NewGuid();
        var btc = new Asset
        {
            Id = btcId,
            Symbol = "BTCUSDT",
            Name = "Bitcoin",
            MarketType = MarketType.Crypto,
            IsActive = true,
        };
        var candles = Enumerable.Range(0, 600).Select(i => new Candle
        {
            AssetId = btcId,
            OpenTime = Start.AddDays(i),
            Open = i + 1, High = i + 1, Low = i + 1, Close = i + 1,
            Volume = 1,
            Interval = CandleInterval.OneDay
        }).ToList();
        var matches = new List<HistoricalMatchDto> { Match(Start.AddDays(100), 50.0) };

        var assets = new Mock<IAssetRepository>(MockBehavior.Strict);
        assets.Setup(a => a.GetBySymbolAsync("BTCUSDT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(btc);

        var candleRepo = new Mock<ICandleRepository>(MockBehavior.Strict);
        candleRepo.Setup(c => c.GetDailyHistoryAsync(btcId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(candles);

        // Strict mock: the service must ask for BOTH horizon profiles (long for
        // 3M/6M/breakdown, short for 1M), each floored at the first candle's
        // date — any other arguments throw.
        var regime = new Mock<IMacroRegimeService>(MockBehavior.Strict);
        regime.Setup(r => r.FindSimilarPeriodsAsync(
                MatchingOptions.AnalogCount, MatchingOptions.CryptoProduction, Start, It.IsAny<CancellationToken>()))
            .ReturnsAsync(matches);
        regime.Setup(r => r.FindSimilarPeriodsAsync(
                MatchingOptions.AnalogCount, MatchingOptions.CryptoShortHorizon, Start, It.IsAny<CancellationToken>()))
            .ReturnsAsync(matches);

        var explainer = new Mock<IAssetExplainerService>();
        explainer.Setup(e => e.GenerateAsync("BTCUSDT", MarketType.Crypto, matches, candles, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["crypto bullet"]);

        var service = new HistoricalOddsService(assets.Object, candleRepo.Object, regime.Object, explainer.Object);

        var result = await service.GetOddsAsync("BTCUSDT");

        Assert.Equal("BTCUSDT", result.Symbol);
        Assert.Equal(600m, result.CurrentPrice);
        regime.Verify(r => r.FindSimilarPeriodsAsync(
            MatchingOptions.AnalogCount, MatchingOptions.CryptoProduction, Start, It.IsAny<CancellationToken>()), Times.Once);
        regime.Verify(r => r.FindSimilarPeriodsAsync(
            MatchingOptions.AnalogCount, MatchingOptions.CryptoShortHorizon, Start, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOddsAsync_IncludesExplainerBullets()
    {
        var matches = new List<HistoricalMatchDto> { Match(Start.AddDays(100), 50.0) };
        var service = BuildService(RisingCandles(), matches, out _);

        var result = await service.GetOddsAsync("SPY");

        Assert.Equal(["macro bullet"], result.Explanations);
        Assert.Equal(600m, result.CurrentPrice);
    }

    [Fact]
    public async Task GetOddsAsync_BreakdownSplitsAnalogsByMa200State()
    {
        var candles = RisingCandles();
        // Analog 1 at index 100: fewer than 200 prior candles => state unknown (null).
        // Analog 2 at index 200: rising series => price above its 200-day average.
        var matches = new List<HistoricalMatchDto>
        {
            Match(Start.AddDays(100), 50.0),
            Match(Start.AddDays(200), 50.0),
        };
        var service = BuildService(candles, matches, out _);

        var result = await service.GetOddsAsync("SPY");
        var breakdown = result.Breakdown;

        Assert.NotNull(breakdown);
        Assert.Equal(true, breakdown.CurrentAboveMa200); // rising series ends above its MA

        // Only the index-200 analog has a known state; its 3M return: (291-201)/201 = 44.78%
        Assert.Equal(1, breakdown.AboveCount);
        Assert.Equal(100.0, breakdown.AboveOdds3M);
        Assert.Equal(44.78m, breakdown.AboveMedian3M);
        Assert.Equal(0, breakdown.BelowCount);
        Assert.Null(breakdown.BelowOdds3M);
        Assert.Null(breakdown.BelowMedian3M);

        Assert.Equal(2, breakdown.Points.Count);
        Assert.Equal(Start.AddDays(100), breakdown.Points[0].Date);
        Assert.Null(breakdown.Points[0].AboveMa200);
        Assert.Equal(Start.AddDays(200), breakdown.Points[1].Date);
        Assert.Equal(true, breakdown.Points[1].AboveMa200);
    }

    [Fact]
    public async Task GetOddsAsync_UnknownSymbol_ThrowsWithExactMessage()
    {
        var assets = new Mock<IAssetRepository>();
        assets.Setup(a => a.GetBySymbolAsync("NOPE", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Asset?)null);

        var service = new HistoricalOddsService(
            assets.Object,
            Mock.Of<ICandleRepository>(),
            Mock.Of<IMacroRegimeService>(),
            Mock.Of<IAssetExplainerService>());

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => service.GetOddsAsync("NOPE"));
        Assert.Equal("Asset NOPE not found", ex.Message);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(366)]
    public async Task GetOddsForDaysAsync_OutOfRangeDays_Throws(int days)
    {
        var service = BuildService(RisingCandles(), [], out _);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.GetOddsForDaysAsync("SPY", days));
    }
}
