namespace RegimeDeck.Domain.Entities;

/// One row per asset view. Powers "most searched" lists and tells us which
/// asset classes users actually care about.
public class SearchLog : BaseEntity
{
    public string Symbol { get; set; } = string.Empty;   // normalized, e.g. BTCUSDT
    public string? RawQuery { get; set; }                 // what the user typed, e.g. "bitcoin"
    public string? Source { get; set; }                   // search | autocomplete | popular | direct
    public Guid? UserId { get; set; }
    public bool WasGated { get; set; }                    // hit the signup wall on this view
    public DateTime CreatedAt { get; set; }
}
