namespace RegimeDeck.Application.Sectors;

/// The 11 S&P sectors (SPDR "Select Sector" ETFs) and the benchmark their
/// relative strength is measured against. Explicit and fixed — this is the
/// standard sector map, not a user-configurable universe.
public static class SectorUniverse
{
    public const string Benchmark = "SPY";

    /// Lookback for the relative-strength calc, aligned with the 3-month odds.
    public const int RelStrengthLookbackDays = 90;

    public static readonly IReadOnlyList<(string Symbol, string Name)> Sectors =
    [
        ("XLK", "Technology"),
        ("XLF", "Financials"),
        ("XLE", "Energy"),
        ("XLV", "Health Care"),
        ("XLI", "Industrials"),
        ("XLY", "Consumer Discretionary"),
        ("XLP", "Consumer Staples"),
        ("XLU", "Utilities"),
        ("XLB", "Materials"),
        ("XLRE", "Real Estate"),
        ("XLC", "Communication Services"),
    ];
}
