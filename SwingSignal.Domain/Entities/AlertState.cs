namespace SwingSignal.Domain.Entities;

/// Last-known value of an alert-watched condition ("health", "stance:SPY").
/// Alerts fire on the delta between this and the freshly computed value —
/// global, not per-user, because the underlying readings are the same for
/// everyone.
public class AlertState : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}
