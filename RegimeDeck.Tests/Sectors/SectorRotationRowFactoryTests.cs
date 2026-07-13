using RegimeDeck.Application.Sectors;
using RegimeDeck.Contracts.Regime;

namespace RegimeDeck.Tests.Sectors;

public class SectorRotationRowFactoryTests
{
    private static readonly DateTime At = new(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);

    private static OddsForPeriodDto Period(int cases, double odds = 64, double baseRate = 55, double edge = 9) =>
        new(cases, 20, odds, 3m, 3m, 25m, -12m, null, null, null, cases > 0 ? baseRate : null, cases > 0 ? edge : 0);

    private static AssetOddsDto Odds(OddsForPeriodDto threeMonths, TradeReadDto? read) =>
        new("XLK", "Technology ETF", 40, 250m, Period(30), threeMonths, Period(30), [], "d", null, read);

    [Fact]
    public void MapsOddsRelStrengthAndFriendlySectorName()
    {
        var read = new TradeReadDto("Long bias", 90, "Strong", ["reason"], "note");
        var row = SectorRotationRowFactory.Create(
            "XLK", "Technology", Odds(Period(30, 64, 55, 9), read), relStrength: 4.2, At);

        Assert.Equal("XLK", row.Symbol);
        Assert.Equal("Technology", row.Sector);   // friendly name, not the ETF's raw name
        Assert.Equal(64, row.Odds3M);
        Assert.Equal(9, row.Edge3M);
        Assert.Equal("Long bias", row.Stance);
        Assert.Equal(4.2, row.RelStrength3M);
        Assert.Equal(At, row.ComputedAt);
    }

    [Fact]
    public void NoComputableOdds_LeavesRegimeFieldsNull_KeepsRelStrength()
    {
        var row = SectorRotationRowFactory.Create(
            "XLE", "Energy", Odds(Period(0), read: null), relStrength: -3.1, At);

        Assert.Null(row.Odds3M);
        Assert.Null(row.Edge3M);
        Assert.Null(row.Stance);
        Assert.Equal(-3.1, row.RelStrength3M); // momentum still computable from candles
    }
}
