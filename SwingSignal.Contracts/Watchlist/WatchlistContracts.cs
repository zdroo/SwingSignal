using SwingSignal.Contracts.Regime;

namespace SwingSignal.Contracts.Watchlist;

public record AddWatchlistRequest(string Symbol);

public record WatchlistItemDto(string Symbol, string Name, DateTime AddedAt);

/// One watchlist row with the asset's current statistical picture — the
/// same numbers the odds page shows, condensed to one line.
public record WatchlistRowDto(
    string Symbol,
    string Name,
    decimal? CurrentPrice,
    double? Odds3M,       // null while the asset has no computable odds yet
    double? BaseRate3M,
    double? Edge3M,
    TradeReadDto? TradeRead,
    DateTime AddedAt);
