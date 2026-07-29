using RegimeDeck.Application.Screener;
using RegimeDeck.Contracts.Regime;
using RegimeDeck.Domain.Entities;
using RegimeDeck.Domain.Enums;

namespace RegimeDeck.Tests.Screener;

public class ScreenerRowFactoryTests
{
    private static readonly DateTime At = new(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);

    private static Asset Asset(string symbol = "SPY") =>
        new() { Symbol = symbol, Name = "S&P 500 ETF", MarketType = MarketType.Index };

    private static OddsForPeriodDto Period(int cases, double odds = 69, double baseRate = 59, double edge = 10) =>
        new(cases, 20, odds, 3m, 3m, 25m, -12m, cases > 0 ? baseRate : null, cases > 0 ? edge : 0);

    private static AssetOddsDto Odds(OddsForPeriodDto threeMonths, TradeReadDto? read) =>
        new("SPY", "S&P 500 ETF", 40, 755m, Period(30), threeMonths, Period(30), [], "d", null, read);

    [Fact]
    public void MapsOdds3MAndReadOntoRow()
    {
        var read = new TradeReadDto("Long bias", 90, "Strong", ["reason"], "note");
        var row = ScreenerRowFactory.Create(Asset(), Odds(Period(30, 69, 59, 10), read), At);

        Assert.Equal("SPY", row.Symbol);
        Assert.Equal(MarketType.Index, row.MarketType);
        Assert.Equal(755m, row.CurrentPrice);
        Assert.Equal(69, row.Odds3M);
        Assert.Equal(59, row.BaseRate3M);
        Assert.Equal(10, row.Edge3M);
        Assert.Equal("Long bias", row.Stance);
        Assert.Equal("Strong", row.Strength);
        Assert.Equal(At, row.ComputedAt);
    }

    [Fact]
    public void NoComputableOdds_LeavesNumbersNull()
    {
        var row = ScreenerRowFactory.Create(Asset(), Odds(Period(0), read: null), At);

        Assert.Null(row.Odds3M);
        Assert.Null(row.BaseRate3M);
        Assert.Null(row.Edge3M);
    }

    [Fact]
    public void NullTradeRead_LeavesStanceNull()
    {
        var row = ScreenerRowFactory.Create(Asset(), Odds(Period(30), read: null), At);

        Assert.Null(row.Stance);
        Assert.Null(row.Strength);
    }
}
