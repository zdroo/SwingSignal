using RegimeDeck.Application.Common;
using RegimeDeck.Domain.Entities;
using RegimeDeck.Domain.Enums;

namespace RegimeDeck.Application.Screener;

/// The read-side rules: which rows a caller sees (free teaser vs full),
/// how filters apply (Pro only), and the ranking. Pure and total — the
/// single place the free/Pro boundary and the sort order live.
public static class ScreenerProjection
{
    public static IReadOnlyList<ScreenerRow> Project(
        IReadOnlyList<ScreenerRow> rows, bool isPro, ScreenerQuery? query)
    {
        IEnumerable<ScreenerRow> result = rows;

        if (!isPro)
        {
            // Free sees a fixed teaser subset; filters are a Pro affordance
            result = result.Where(r => ScreenerUniverse.FreeSymbols.Contains(r.Symbol));
        }
        else if (query is not null)
        {
            if (!string.IsNullOrWhiteSpace(query.Stance))
                result = result.Where(r =>
                    string.Equals(r.Stance, query.Stance, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(query.MarketType)
                && Enum.TryParse<MarketType>(query.MarketType, ignoreCase: true, out var market))
                result = result.Where(r => r.MarketType == market);

            if (query.MinEdge is double minEdge)
                result = result.Where(r => r.Edge3M is double edge && edge >= minEdge);
        }

        return result.ByEdgeDescending(r => r.Edge3M, r => r.Symbol).ToList();
    }
}
