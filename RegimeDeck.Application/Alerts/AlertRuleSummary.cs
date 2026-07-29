using System.Globalization;
using RegimeDeck.Contracts.Alerts;

namespace RegimeDeck.Application.Alerts;

/// Human-readable one-liner for a rule, e.g.
/// "VIX above 30 AND SPY below its 200-day average".
public static class AlertRuleSummary
{
    public static string Describe(IReadOnlyList<AlertConditionDto> conditions) =>
        conditions.Count == 0 ? "No conditions" : string.Join(" AND ", conditions.Select(Describe));

    private static string Describe(AlertConditionDto c)
    {
        var op = c.Operator.ToLowerInvariant();
        return c.Type switch
        {
            AlertConditionTypes.MacroIndicator => $"{c.Subject} {op} {Num(c.Threshold)}",
            AlertConditionTypes.AssetPrice => $"{c.Subject} price {op} {Num(c.Threshold)}",
            AlertConditionTypes.MovingAverage => $"{c.Subject} {op} its {c.Param}-day average",
            AlertConditionTypes.VolumeSpike => $"{c.Subject} volume {op} {Num(c.Threshold)}× its {c.Param}-day average",
            _ => $"{c.Subject} {op} {Num(c.Threshold)}",
        };
    }

    private static string Num(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
}
