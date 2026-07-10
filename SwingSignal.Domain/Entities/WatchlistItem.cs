namespace SwingSignal.Domain.Entities;

/// One asset a user tracks on their watchlist (Pro feature). Name is
/// denormalized at add time so listing never needs the asset table.
public class WatchlistItem : BaseEntity
{
    public Guid UserId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
