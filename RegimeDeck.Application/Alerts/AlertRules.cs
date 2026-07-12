using RegimeDeck.Contracts.Regime;

namespace RegimeDeck.Application.Alerts;

/// What counts as an alert-worthy change. Pure comparisons against the
/// stored last-known state — fully unit-testable. A change with no previous
/// state records silently (Notify=false): the first evaluation after
/// deployment must not blast everyone with "changes" from nothing.
public static class AlertRules
{
    public const string HealthKey = "health";
    public static string StanceKey(string symbol) => $"stance:{symbol}";

    public sealed record Change(string Key, string NewValue, bool Notify, string Title, string Detail);

    public static Change? HealthChange(string? previous, MarketHealthDto health)
    {
        if (health.Groups.Count == 0) return null; // no data — nothing to compare
        if (previous == health.Label) return null;

        return new Change(
            HealthKey,
            health.Label,
            Notify: previous is not null,
            Title: previous is null
                ? $"Market Health: {health.Label}"
                : $"Market Health moved: {previous} → {health.Label} ({health.Score}/100)",
            Detail: "The combined indicator picture crossed a band. See the full breakdown on the Live Macro page.");
    }

    public static Change? StanceChange(string symbol, string? previous, TradeReadDto? read)
    {
        if (read is null) return null; // no computable odds — keep the old state
        if (previous == read.Stance) return null;

        return new Change(
            StanceKey(symbol),
            read.Stance,
            Notify: previous is not null,
            Title: previous is null
                ? $"{symbol} statistical read: {read.Stance}"
                : $"{symbol} statistical read flipped: {previous} → {read.Stance}",
            Detail: read.Reasons.Count > 0 ? read.Reasons[0] : "See the asset page for the full picture.");
    }
}
