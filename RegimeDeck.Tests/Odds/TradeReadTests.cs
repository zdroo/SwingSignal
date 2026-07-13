using RegimeDeck.Application.Odds;
using RegimeDeck.Contracts.Regime;

namespace RegimeDeck.Tests.Odds;

// The trade read is a user-facing promise: ≥5pt edge with ≥55% odds is a
// long bias, ≤−5pt with nothing positive is stand aside, anything else is
// no edge — and thin samples always weaken the read. Pinned here exactly.
public class TradeReadTests
{
    private static OddsForPeriodDto Period(
        double odds, double baseRate, decimal median = 3m, decimal worst = -12m, int cases = 30) =>
        new(
            TotalCases: cases,
            PositiveCases: (int)(cases * odds / 100),
            PositiveOdds: odds,
            AverageReturn: median,
            MedianReturn: median,
            BestCase: 25m,
            WorstCase: worst,
            PriceTargetLow: null,
            PriceTargetMid: null,
            PriceTargetHigh: null,
            BaseRate: baseRate,
            Edge: Math.Round(odds - baseRate, 1));

    private static readonly OddsForPeriodDto Empty =
        new(0, 0, 0, 0, 0, 0, 0, null, null, null);

    [Fact]
    public void ConsistentPositiveEdge_LongBias_Strong()
    {
        var read = TradeRead.Compute(
            Period(64, 55), Period(69, 59), Period(66, 58), matchesUsed: 34);

        Assert.NotNull(read);
        Assert.Equal("Long bias", read.Stance);
        Assert.Equal(90, read.HorizonDays); // the strongest edge (+10) is at 3 months
        Assert.Equal("Strong", read.Strength);
        Assert.Contains(read.Reasons, r => r.Contains("+10 pt regime edge"));
        Assert.Contains(read.Reasons, r => r.Contains("holds at every horizon"));
        Assert.Contains(read.Reasons, r => r.Contains("solid sample"));
    }

    [Fact]
    public void ConsistentNegativeEdge_StandAside()
    {
        var read = TradeRead.Compute(
            Period(48, 54), Period(45, 53, median: -2m), Period(49, 56), matchesUsed: 30);

        Assert.NotNull(read);
        Assert.Equal("Stand aside", read.Stance);
        Assert.Equal(90, read.HorizonDays); // keys on the most negative edge (−8)
        Assert.Equal("Strong", read.Strength);
        Assert.Contains(read.Reasons, r => r.Contains("below the asset's normal"));
        Assert.Contains(read.Reasons, r => r.Contains("below the base rate at every horizon"));
    }

    [Fact]
    public void SmallEdges_NoEdge()
    {
        var read = TradeRead.Compute(
            Period(57, 55), Period(56, 57), Period(60, 57), matchesUsed: 30);

        Assert.NotNull(read);
        Assert.Equal("No edge", read.Stance);
        Assert.Equal("Weak", read.Strength);
        Assert.Contains(read.Reasons, r => r.Contains("indistinguishable from any other day"));
    }

    [Fact]
    public void GoodEdgeButCoinFlipOdds_NoEdge()
    {
        // +6pt edge, but odds only reach 52% — nothing to act on
        var read = TradeRead.Compute(
            Period(50, 48), Period(52, 46), Period(49, 47), matchesUsed: 30);

        Assert.NotNull(read);
        Assert.Equal("No edge", read.Stance);
        Assert.Contains(read.Reasons, r => r.Contains("coin flip"));
    }

    [Fact]
    public void ThinSample_AlwaysWeak_WithWarning()
    {
        var read = TradeRead.Compute(
            Period(68, 56), Period(70, 58), Period(69, 57), matchesUsed: 12);

        Assert.NotNull(read);
        Assert.Equal("Long bias", read.Stance);
        Assert.Equal("Weak", read.Strength);
        Assert.Contains(read.Reasons, r => r.Contains("thin evidence"));
    }

    [Fact]
    public void EdgeAtOneHorizonOnly_NamesThatHorizon()
    {
        var read = TradeRead.Compute(
            Period(63, 56), Period(58, 57), Period(57, 56), matchesUsed: 30);

        Assert.NotNull(read);
        Assert.Equal("Long bias", read.Stance);
        Assert.Equal(30, read.HorizonDays);
        Assert.Contains(read.Reasons, r => r.Contains("specific to the 1-month horizon"));
        Assert.Equal("Moderate", read.Strength); // +7 edge, solid sample, but not ≥8
    }

    [Fact]
    public void MixedDirections_CautionReason()
    {
        var read = TradeRead.Compute(
            Period(64, 56), Period(48, 55), Period(58, 56), matchesUsed: 30);

        Assert.NotNull(read);
        Assert.Equal("Long bias", read.Stance); // best edge +8 with 64% odds still wins
        Assert.Contains(read.Reasons, r => r.Contains("horizons disagree"));
    }

    [Fact]
    public void NoUsableHorizons_ReturnsNull()
    {
        Assert.Null(TradeRead.Compute(Empty, Empty, Empty, matchesUsed: 0));
    }

    [Fact]
    public void RiskLineAlwaysPresent_WithMedianAndWorstCase()
    {
        var read = TradeRead.Compute(
            Period(64, 55, median: 6.2m, worst: -18.4m),
            Period(58, 56), Period(57, 56), matchesUsed: 30);

        Assert.NotNull(read);
        Assert.Contains(read.Reasons, r =>
            r.Contains("median analog outcome +6.2%") && r.Contains("worst case -18.4%"));
    }
}
