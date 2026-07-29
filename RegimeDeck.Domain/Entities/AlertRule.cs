namespace RegimeDeck.Domain.Entities;

/// A user-defined alert: a set of conditions (combined with AND) that, when they
/// first all become true, emails the owner. Edge-triggered via LastMet so it
/// fires once per crossing, not every evaluation.
public class AlertRule : BaseEntity
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;

    /// Serialized List<AlertConditionDto>.
    public string ConditionsJson { get; set; } = "[]";

    /// Whether the combined condition held at the last evaluation (rearm state).
    public bool LastMet { get; set; }
    public DateTime? LastTriggeredAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
