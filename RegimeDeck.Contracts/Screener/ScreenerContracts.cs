namespace RegimeDeck.Contracts.Screener;

/// One row of the regime screener — the asset page's headline read for a
/// single asset, precomputed so the whole board loads at once.
public record ScreenerRowDto(
    string Symbol,
    string Name,
    string MarketType,
    decimal? CurrentPrice,
    double? Odds3M,
    double? BaseRate3M,
    double? Edge3M,
    string? Stance,
    string? Strength);

public record ScreenerResultDto(
    List<ScreenerRowDto> Rows,
    DateTime? AsOf,       // newest ComputedAt across the board; null if never computed
    int UniverseSize,     // total assets scanned (what Pro sees)
    bool Trimmed);        // true = free teaser view (a subset of the universe)
