using RegimeDeck.Application.Alerts;
using RegimeDeck.Contracts.Alerts;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Tests.Alerts;

public class AlertConditionEvaluatorTests
{
    private static List<Candle> Candles(decimal[] closes, decimal[]? volumes = null)
    {
        var list = new List<Candle>();
        for (var i = 0; i < closes.Length; i++)
            list.Add(new Candle { OpenTime = new DateTime(2026, 1, 1).AddDays(i), Close = closes[i], Volume = volumes?[i] ?? 100m });
        return list;
    }

    private static AlertData Data(
        Dictionary<string, double>? macro = null, Dictionary<string, List<Candle>>? candles = null) =>
        new() { Macro = macro ?? [], Candles = candles ?? [] };

    private static AlertConditionDto Cond(string type, string subject, string op, double threshold = 0, int param = 0) =>
        new(type, subject, op, threshold, param);

    [Theory]
    [InlineData("Above", 30, true)]
    [InlineData("Above", 40, false)]
    [InlineData("Below", 30, false)]
    [InlineData("Below", 40, true)]
    public void MacroIndicator_ComparesLatestValue(string op, double threshold, bool expected)
    {
        var data = Data(macro: new() { ["VIX"] = 35 });
        Assert.Equal(expected, AlertConditionEvaluator.Evaluate(
            Cond(AlertConditionTypes.MacroIndicator, "VIX", op, threshold), data));
    }

    [Fact]
    public void MacroIndicator_MissingValue_IsNull()
    {
        Assert.Null(AlertConditionEvaluator.Evaluate(
            Cond(AlertConditionTypes.MacroIndicator, "CPI", "Above", 3), Data()));
    }

    [Fact]
    public void AssetPrice_ComparesLatestClose()
    {
        var data = Data(candles: new() { ["SPY"] = Candles([100m, 130m]) });
        Assert.True(AlertConditionEvaluator.Evaluate(Cond(AlertConditionTypes.AssetPrice, "SPY", "Above", 120), data));
        Assert.False(AlertConditionEvaluator.Evaluate(Cond(AlertConditionTypes.AssetPrice, "SPY", "Below", 120), data));
    }

    [Fact]
    public void MovingAverage_ComparesCloseToSma()
    {
        var closes = Enumerable.Repeat(100m, 19).Append(130m).ToArray(); // SMA(20) = 101.5
        var data = Data(candles: new() { ["SPY"] = Candles(closes) });

        Assert.True(AlertConditionEvaluator.Evaluate(Cond(AlertConditionTypes.MovingAverage, "SPY", "Above", param: 20), data));
        Assert.False(AlertConditionEvaluator.Evaluate(Cond(AlertConditionTypes.MovingAverage, "SPY", "Below", param: 20), data));
    }

    [Fact]
    public void MovingAverage_NotEnoughHistory_IsNull()
    {
        var data = Data(candles: new() { ["SPY"] = Candles([100m, 101m]) });
        Assert.Null(AlertConditionEvaluator.Evaluate(Cond(AlertConditionTypes.MovingAverage, "SPY", "Above", param: 200), data));
    }

    [Fact]
    public void VolumeSpike_ComparesRatioToAverage()
    {
        var closes = Enumerable.Repeat(100m, 20).ToArray();
        var volumes = Enumerable.Repeat(100m, 19).Append(300m).ToArray(); // avg=110, ratio≈2.73
        var data = Data(candles: new() { ["BTCUSDT"] = Candles(closes, volumes) });

        Assert.True(AlertConditionEvaluator.Evaluate(Cond(AlertConditionTypes.VolumeSpike, "BTCUSDT", "Above", 2, 20), data));
        Assert.False(AlertConditionEvaluator.Evaluate(Cond(AlertConditionTypes.VolumeSpike, "BTCUSDT", "Above", 3, 20), data));
    }

    [Fact]
    public void RuleMet_AndSemantics_AndNullPropagation()
    {
        var data = Data(
            macro: new() { ["VIX"] = 35 },
            candles: new() { ["SPY"] = Candles([100m, 130m]) });

        // VIX>30 AND SPY<200 → both true
        Assert.True(AlertConditionEvaluator.RuleMet(
            [Cond(AlertConditionTypes.MacroIndicator, "VIX", "Above", 30),
             Cond(AlertConditionTypes.AssetPrice, "SPY", "Below", 200)], data));

        // one false → false
        Assert.False(AlertConditionEvaluator.RuleMet(
            [Cond(AlertConditionTypes.MacroIndicator, "VIX", "Above", 30),
             Cond(AlertConditionTypes.AssetPrice, "SPY", "Above", 200)], data));

        // references missing data → null (rule skipped, not fired)
        Assert.Null(AlertConditionEvaluator.RuleMet(
            [Cond(AlertConditionTypes.MacroIndicator, "CPI", "Above", 3)], data));
    }

    [Fact]
    public void Summary_ReadsAsPlainEnglish()
    {
        var summary = AlertRuleSummary.Describe(
        [
            Cond(AlertConditionTypes.MacroIndicator, "VIX", "Above", 30),
            Cond(AlertConditionTypes.MovingAverage, "SPY", "Below", param: 200),
        ]);
        Assert.Equal("VIX above 30 AND SPY below its 200-day average", summary);
    }
}
