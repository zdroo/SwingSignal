namespace RegimeDeck.Application.Common;

public static class EdgeRanking
{
    /// Orders rows by regime edge, biggest first, with rows lacking a
    /// computable edge sunk to the bottom, then a stable ordinal tiebreak.
    /// The one place the screener and sector boards agree on ranking.
    public static IEnumerable<T> ByEdgeDescending<T>(
        this IEnumerable<T> rows, Func<T, double?> edge, Func<T, string> tiebreak) =>
        rows
            .OrderByDescending(r => edge(r).HasValue)
            .ThenByDescending(r => edge(r) ?? double.MinValue)
            .ThenBy(tiebreak, StringComparer.Ordinal);
}
