using RegimeDeck.Application.Screener;
using RegimeDeck.Domain.Entities;
using RegimeDeck.Domain.Enums;

namespace RegimeDeck.Tests.Screener;

// The read-side contract: free callers see only the teaser subset (filters
// ignored), Pro sees the whole board with filters, and rows always rank by
// edge with no-read rows sinking last.
public class ScreenerProjectionTests
{
    private static ScreenerRow Row(
        string symbol, double? edge, string? stance = "No edge",
        MarketType market = MarketType.Index) =>
        new()
        {
            Symbol = symbol,
            Name = symbol,
            MarketType = market,
            Edge3M = edge,
            Stance = stance,
            ComputedAt = DateTime.UtcNow,
        };

    private static readonly List<ScreenerRow> Board =
    [
        Row("BTCUSDT", 3.0, market: MarketType.Crypto),   // free
        Row("SPY", 8.0),                                  // free
        Row("GLD", -2.0, market: MarketType.Commodity),   // free
        Row("AAPL", 12.0, stance: "Long bias", market: MarketType.Stock), // Pro-only
        Row("NVDA", null, stance: null, market: MarketType.Stock),        // Pro-only, no read
    ];

    [Fact]
    public void Free_ShowsOnlyTeaserSubset()
    {
        var result = ScreenerProjection.Project(Board, isPro: false, query: null);

        Assert.All(result, r => Assert.Contains(r.Symbol, ScreenerUniverse.FreeSymbols));
        Assert.DoesNotContain(result, r => r.Symbol == "AAPL"); // not in free set
    }

    [Fact]
    public void Free_IgnoresFilters()
    {
        // A filter passed by a non-Pro caller must not leak the full board
        var result = ScreenerProjection.Project(
            Board, isPro: false, new ScreenerQuery(Stance: "Long bias", null, null));

        Assert.All(result, r => Assert.Contains(r.Symbol, ScreenerUniverse.FreeSymbols));
    }

    [Fact]
    public void Pro_NoQuery_ReturnsWholeBoard()
    {
        var result = ScreenerProjection.Project(Board, isPro: true, query: null);
        Assert.Equal(Board.Count, result.Count);
    }

    [Fact]
    public void RanksByEdgeDescending_NoReadRowsLast()
    {
        var result = ScreenerProjection.Project(Board, isPro: true, query: null);

        Assert.Equal("AAPL", result[0].Symbol);   // +12
        Assert.Equal("SPY", result[1].Symbol);     // +8
        Assert.Equal("BTCUSDT", result[2].Symbol); // +3
        Assert.Equal("GLD", result[3].Symbol);     // -2
        Assert.Equal("NVDA", result[^1].Symbol);   // null edge → last
    }

    [Fact]
    public void Pro_StanceFilter()
    {
        var result = ScreenerProjection.Project(
            Board, isPro: true, new ScreenerQuery(Stance: "Long bias", null, null));

        Assert.Equal(["AAPL"], result.Select(r => r.Symbol));
    }

    [Fact]
    public void Pro_MarketFilter_CaseInsensitive()
    {
        var result = ScreenerProjection.Project(
            Board, isPro: true, new ScreenerQuery(null, MarketType: "stock", null));

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(MarketType.Stock, r.MarketType));
    }

    [Fact]
    public void Pro_MinEdgeFilter_ExcludesNullAndBelowThreshold()
    {
        var result = ScreenerProjection.Project(
            Board, isPro: true, new ScreenerQuery(null, null, MinEdge: 5.0));

        Assert.Equal(["AAPL", "SPY"], result.Select(r => r.Symbol)); // 12, 8; ranked
    }

    [Fact]
    public void UnknownMarketFilter_IsIgnored_NotAnError()
    {
        var result = ScreenerProjection.Project(
            Board, isPro: true, new ScreenerQuery(null, MarketType: "banana", null));

        Assert.Equal(Board.Count, result.Count); // unparseable filter = no filter
    }
}
