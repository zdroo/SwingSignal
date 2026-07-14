using System.Globalization;
using RegimeDeck.Contracts.Regime;

namespace RegimeDeck.Application.Alerts;

/// What counts as an alert-worthy change. Pure comparisons against the
/// stored last-known state — fully unit-testable. A change with no previous
/// state records silently (Notify=false): the first evaluation after
/// deployment must not blast everyone with "changes" from nothing.
public static class AlertRules
{
    /// An edge at or above this (pp) counts as "meaningfully positive" — small
    /// enough to matter, large enough not to fire on noise near zero.
    public const double EdgePositiveThreshold = 3.0;

    public const string HealthKey = "health";
    public static string StanceKey(string symbol) => $"stance:{symbol}";
    public static string EdgeKey(string symbol) => $"edge:{symbol}";
    public static string PriceZoneKey(string symbol) => $"pricezone:{symbol}";

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

    /// The regime edge crossing into (or out of) meaningfully-positive
    /// territory. Only the flat → up transition is worth an email; the reverse
    /// updates the stored state silently so the next upturn can fire again.
    public static Change? EdgeChange(string symbol, string? previous, double? edge)
    {
        if (edge is null) return null; // no computable edge — keep the old state

        var state = edge >= EdgePositiveThreshold ? "up" : "flat";
        if (previous == state) return null;

        return new Change(
            EdgeKey(symbol),
            state,
            Notify: previous is not null && state == "up",
            Title: $"{symbol}: regime edge turned positive (now {Signed(edge.Value)}pp)",
            Detail: "Conditions like today are now adding to this asset's odds versus its base rate. "
                + "Descriptive, not a buy signal — see the asset page.");
    }

    /// The price crossing above its optimistic (P75) or below its conservative
    /// (P25) 3-month target. Returning into the normal range updates state
    /// silently — only breakouts either side are news.
    public static Change? PriceZoneChange(
        string symbol, string? previous, decimal? price, decimal? conservative, decimal? optimistic)
    {
        if (price is null || conservative is null || optimistic is null) return null;

        var zone = price >= optimistic ? "above" : price <= conservative ? "below" : "mid";
        if (previous == zone) return null;

        var notify = previous is not null && zone != "mid";
        return new Change(
            PriceZoneKey(symbol),
            zone,
            Notify: notify,
            Title: zone == "above"
                ? $"{symbol} pushed above its optimistic 3-month price target"
                : zone == "below"
                ? $"{symbol} fell below its conservative 3-month price target"
                : $"{symbol} is back within its 3-month price range",
            Detail: "The target range is the middle-half of historical outcomes after conditions like "
                + "today's — not a prediction. See the asset page for the numbers.");
    }

    private static string Signed(double value) =>
        (value >= 0 ? "+" : "") + value.ToString("0.0", CultureInfo.InvariantCulture);
}
