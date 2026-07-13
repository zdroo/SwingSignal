namespace RegimeDeck.Application.Screener;

/// The curated set of assets the screener scans. Deliberately explicit (not
/// "every asset in the DB") so user-searched symbols never balloon the board
/// and the compute cost stays bounded. Forex is excluded — the screener
/// answers "what would I go long", which pairs don't map onto cleanly.
/// Phase 2 (sector rotation) will append the SPDR sector ETFs.
public static class ScreenerUniverse
{
    public static readonly IReadOnlyList<string> Symbols =
    [
        "BTCUSDT", "ETHUSDT",          // crypto majors
        "SPY", "QQQ", "IWM", "DIA",    // equity indices
        "GLD", "SLV", "USO",           // commodities
        "TLT", "HYG",                  // rates / credit
        "AAPL", "MSFT", "NVDA",        // mega-cap stocks
    ];

    /// The free teaser: a diverse handful so anonymous visitors see real
    /// value while the full board stays a Pro feature.
    public static readonly IReadOnlySet<string> FreeSymbols =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "BTCUSDT", "ETHUSDT", "SPY", "QQQ", "GLD", "TLT",
        };
}
