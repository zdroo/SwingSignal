namespace RegimeDeck.Domain.Entities;

/// One precomputed sector-rotation cell: an S&P sector's regime fit (the odds
/// engine's read of its ETF) plus its recent relative strength versus the
/// broad market. Refreshed by the background compute service; the sectors
/// page reads only these cached rows.
public class SectorRotationRow : BaseEntity
{
    public string Symbol { get; set; } = string.Empty;  // sector ETF, e.g. XLK
    public string Sector { get; set; } = string.Empty;  // friendly name, e.g. Technology

    // Regime fit — same numbers the asset page/screener show for the ETF
    public double? Odds3M { get; set; }
    public double? Edge3M { get; set; }
    public string? Stance { get; set; }

    // Momentum — % the sector out/under-performed the benchmark over ~3 months
    public double? RelStrength3M { get; set; }

    public DateTime ComputedAt { get; set; }
}
