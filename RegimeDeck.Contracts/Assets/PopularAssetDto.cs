namespace RegimeDeck.Contracts.Assets;

// Compact card data for the "popular assets" strips: 90-day sparkline + 3M odds
public record PopularAssetDto(
    string Symbol,
    string Name,
    decimal CurrentPrice,
    decimal ChangePct,       // over the sparkline window (~90 days)
    List<decimal> Spark,     // downsampled daily closes, oldest first
    double? Odds3M,          // shrunk positive odds for the next 90 days
    double? BaseRate3M,
    double? Edge3M);
