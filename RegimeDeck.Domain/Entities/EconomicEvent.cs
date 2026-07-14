namespace RegimeDeck.Domain.Entities;

/// One scheduled high-impact macro release (CPI, jobs, FOMC, …), sourced from
/// FRED's release calendar and cached so the /events endpoint is a fast read.
/// Global, not per-asset — the whole market watches these.
public class EconomicEvent : BaseEntity
{
    public int ReleaseId { get; set; }              // FRED release id (upsert key with Date)
    public string Title { get; set; } = string.Empty;
    public DateTime Date { get; set; }              // release date (UTC, date-only)
    public string Impact { get; set; } = string.Empty;
}
