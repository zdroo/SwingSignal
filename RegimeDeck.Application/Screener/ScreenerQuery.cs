namespace RegimeDeck.Application.Screener;

/// Pro-only filters over the full board. All optional; null means "no filter".
public record ScreenerQuery(string? Stance, string? MarketType, double? MinEdge);
