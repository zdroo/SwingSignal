using RegimeDeck.Domain.Enums;

namespace RegimeDeck.Domain.Entities;

/// One precomputed screener cell: an asset's current regime read, refreshed
/// by the background compute service. The screener page reads only these
/// cached rows — it never runs the odds engine on a request thread.
public class ScreenerRow : BaseEntity
{
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public MarketType MarketType { get; set; }
    public decimal? CurrentPrice { get; set; }

    // Null when the asset has no computable 3-month odds yet (thin history)
    public double? Odds3M { get; set; }
    public double? BaseRate3M { get; set; }
    public double? Edge3M { get; set; }

    // Mirror the asset page's Statistical Read; null when no read is available
    public string? Stance { get; set; }
    public string? Strength { get; set; }

    public DateTime ComputedAt { get; set; }
}
