namespace RegimeDeck.Domain.Entities;

/// Pro-tier interest, captured before billing exists. The cheapest possible
/// validation of the monetization plan.
public class WaitlistEntry : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string? Source { get; set; }   // where the button was clicked
    public DateTime CreatedAt { get; set; }
}
