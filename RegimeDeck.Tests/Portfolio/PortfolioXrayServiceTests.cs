using Moq;
using RegimeDeck.Application.Abstractions.Ingestion;
using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Application.Common;
using RegimeDeck.Application.Liquidity;
using RegimeDeck.Application.Portfolio;
using RegimeDeck.Application.Regime;
using RegimeDeck.Contracts.Liquidity;
using RegimeDeck.Contracts.Portfolio;
using RegimeDeck.Contracts.Regime;
using RegimeDeck.Domain.Entities;
using RegimeDeck.Domain.Enums;

namespace RegimeDeck.Tests.Portfolio;

public class PortfolioXrayServiceTests
{
    private static readonly DateTime Start = new(2023, 1, 1);

    private readonly Mock<IAssetIngestionService> _ingestion = new();
    private readonly Mock<ICandleRepository> _candles = new();
    private readonly Mock<IMacroRegimeService> _regime = new();
    private readonly Mock<ILiquidityService> _liquidity = new();

    private PortfolioXrayService Service() =>
        new(_ingestion.Object, _candles.Object, _regime.Object, _liquidity.Object);

    // Daily rising candles for ~3 years so analog months have a 90-day exit.
    private static List<Candle> RisingCandles(Guid assetId, decimal start = 100m, decimal step = 0.1m)
    {
        var list = new List<Candle>();
        for (var i = 0; i < 1000; i++)
            list.Add(new Candle { AssetId = assetId, OpenTime = Start.AddDays(i), Close = start + step * i, Interval = CandleInterval.OneDay });
        return list;
    }

    private Asset Register(string symbol, string name, MarketType market)
    {
        var asset = new Asset { Id = Guid.NewGuid(), Symbol = symbol, Name = name, MarketType = market, IsActive = true };
        _ingestion.Setup(i => i.EnsureIngestedAsync(symbol, It.IsAny<CancellationToken>())).ReturnsAsync(asset);
        _candles.Setup(c => c.GetDailyHistoryAsync(asset.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(RisingCandles(asset.Id));
        return asset;
    }

    private void SetupRegime()
    {
        var matches = new List<HistoricalMatchDto>
        {
            new(Start.AddDays(150), 60.0, 10, new()),
            new(Start.AddDays(400), 55.0, 20, new()),
        };
        _regime.Setup(r => r.FindSimilarPeriodsAsync(
                It.IsAny<int>(), It.IsAny<MatchingOptions>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(matches);

        var regime = new MacroRegimeDto(
            new(), new MarketHealthDto(60, "Steady", []), ["A calm macro backdrop."],
            new PlaybookDto("h", "n", []), DateTime.UtcNow);
        _regime.Setup(r => r.GetCurrentRegimeAsync(It.IsAny<CancellationToken>())).ReturnsAsync(regime);

        // Empty liquidity series → per-holding liquidity beta is null (unit-tested separately)
        var reading = new LiquidityReadingDto("", 0, "$T", 0, "Flat", "neutral", "");
        _liquidity.Setup(l => l.GetDashboardAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LiquidityDashboardDto(DateTime.UtcNow, reading, reading, [], reading, reading, [], [], "n"));
    }

    private static PortfolioXrayRequest Request(params (string Symbol, decimal Value)[] holdings) =>
        new([.. holdings.Select(h => new PortfolioHoldingInput(h.Symbol, h.Value))]);

    [Fact]
    public async Task Xray_NormalizesWeights_ClassifiesAndSplitsExposure()
    {
        Register("SPY", "S&P 500 ETF", MarketType.Index);
        Register("GLD", "Gold ETF", MarketType.Commodity);
        SetupRegime();

        var result = await Service().GetXrayAsync(Request(("SPY", 30m), ("GLD", 10m)));

        Assert.Equal(2, result.Holdings.Count);
        var spy = result.Holdings.Single(h => h.Symbol == "SPY");
        var gld = result.Holdings.Single(h => h.Symbol == "GLD");

        Assert.Equal(75.0, spy.WeightPct);
        Assert.Equal(25.0, gld.WeightPct);
        Assert.Equal("Stocks", spy.AssetClass);
        Assert.Equal("Risk-on", spy.Posture);
        Assert.Equal("Gold", gld.AssetClass);         // symbol override, not the Commodity default
        Assert.Equal("Defensive", gld.Posture);

        Assert.Equal(75.0, result.Exposure.RiskOnPct);
        Assert.Equal(25.0, result.Exposure.DefensivePct);
    }

    [Fact]
    public async Task Xray_Concentration_ReflectsWeights()
    {
        Register("SPY", "S&P 500 ETF", MarketType.Index);
        Register("GLD", "Gold ETF", MarketType.Commodity);
        SetupRegime();

        var result = await Service().GetXrayAsync(Request(("SPY", 90m), ("GLD", 10m)));

        Assert.Equal(90.0, result.Concentration.TopWeightPct);
        Assert.Equal(8200, result.Concentration.Hhi); // 90^2 + 10^2
        Assert.Equal("Concentrated", result.Concentration.Label);
        Assert.Contains(result.Concentration.ByClass, c => c.AssetClass == "Stocks" && c.WeightPct == 90.0);
    }

    [Fact]
    public async Task Xray_Outcome_IsDateAlignedAcrossHoldings()
    {
        Register("SPY", "S&P 500 ETF", MarketType.Index);
        Register("GLD", "Gold ETF", MarketType.Commodity);
        SetupRegime();

        var result = await Service().GetXrayAsync(Request(("SPY", 50m), ("GLD", 50m)));

        // Both series rise → every analog month is positive → strong odds
        Assert.True(result.Outcome.Analogs >= 2);
        Assert.True(result.Outcome.MedianReturn > 0);
        Assert.Equal(100.0, result.Outcome.PositiveOddsPct);
        Assert.NotEmpty(result.Reads);
    }

    [Fact]
    public async Task Xray_EmptyHoldings_Throws()
    {
        await Assert.ThrowsAsync<ValidationException>(() => Service().GetXrayAsync(Request()));
    }

    [Fact]
    public async Task Xray_UnknownSymbol_Throws()
    {
        _ingestion.Setup(i => i.EnsureIngestedAsync("NOPE", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Asset?)null);
        SetupRegime();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Service().GetXrayAsync(Request(("NOPE", 10m))));
        Assert.Contains("NOPE", ex.Message);
    }
}
