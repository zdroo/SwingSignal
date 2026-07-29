using RegimeDeck.Contracts.Alerts;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Application.Alerts;

/// The latest data an alert evaluation needs: current macro-indicator values and
/// recent daily candles per symbol (ascending). Assembled by the background
/// evaluator, consumed by the pure evaluator below.
public sealed class AlertData
{
    public required IReadOnlyDictionary<string, double> Macro { get; init; }
    public required IReadOnlyDictionary<string, List<Candle>> Candles { get; init; }
}

/// Pure condition evaluation. Returns null when the condition can't be evaluated
/// yet (missing indicator, no candles, not enough history) — the whole rule then
/// evaluates to null and is skipped rather than mis-fired.
public static class AlertConditionEvaluator
{
    public static bool? Evaluate(AlertConditionDto c, AlertData data)
    {
        switch (c.Type)
        {
            case AlertConditionTypes.MacroIndicator:
                return data.Macro.TryGetValue(c.Subject, out var value)
                    ? Compare(value, c.Operator, c.Threshold) : null;

            case AlertConditionTypes.AssetPrice:
            {
                var candles = Series(data, c.Subject);
                return candles is null ? null : Compare((double)candles[^1].Close, c.Operator, c.Threshold);
            }

            case AlertConditionTypes.MovingAverage:
            {
                var candles = Series(data, c.Subject);
                if (candles is null || c.Param <= 0 || candles.Count < c.Param) return null;
                var sma = (double)candles.TakeLast(c.Param).Average(x => x.Close);
                return Compare((double)candles[^1].Close, c.Operator, sma);
            }

            case AlertConditionTypes.VolumeSpike:
            {
                var candles = Series(data, c.Subject);
                if (candles is null || c.Param <= 0 || candles.Count < c.Param) return null;
                var avgVolume = (double)candles.TakeLast(c.Param).Average(x => x.Volume);
                if (avgVolume <= 0) return null;
                return Compare((double)candles[^1].Volume / avgVolume, c.Operator, c.Threshold);
            }

            default:
                return null;
        }
    }

    /// True only when EVERY condition is met; null when any condition can't be
    /// evaluated (so the rule is skipped, not fired).
    public static bool? RuleMet(IReadOnlyList<AlertConditionDto> conditions, AlertData data)
    {
        if (conditions.Count == 0) return null;

        var all = true;
        foreach (var c in conditions)
        {
            var met = Evaluate(c, data);
            if (met is null) return null;
            if (!met.Value) all = false;
        }
        return all;
    }

    private static List<Candle>? Series(AlertData data, string symbol) =>
        data.Candles.TryGetValue(symbol, out var series) && series.Count > 0 ? series : null;

    private static bool Compare(double value, string op, double threshold) =>
        op == AlertOperators.Above ? value > threshold
        : op == AlertOperators.Below && value < threshold;
}
