namespace RegimeDeck.Application.Events;

/// The curated set of high-impact U.S. macro releases the calendar tracks,
/// keyed by their FRED release id. All are market-movers a swing trader
/// wouldn't want to trade blind into, so they share the "High" impact tag.
public static class EconomicCalendar
{
    public const string HighImpact = "High";

    // FRED release ids whose publication calendar is a clean, scheduled
    // cadence (monthly / quarterly). FOMC is deliberately absent: FRED's
    // "FOMC Press Release" release (101) updates daily, not on the 8-per-year
    // meeting schedule, so it can't source clean meeting dates — that needs a
    // dedicated calendar source, a later enhancement.
    public static readonly IReadOnlyList<(int ReleaseId, string Title)> Releases =
    [
        (10, "CPI — Inflation"),
        (50, "Jobs Report"),
        (54, "PCE — Fed's Inflation Gauge"),
        (53, "GDP"),
    ];
}
