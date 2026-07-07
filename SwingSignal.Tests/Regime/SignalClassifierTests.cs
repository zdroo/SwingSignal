using SwingSignal.Application.Regime;
using SwingSignal.Domain.Enums;

namespace SwingSignal.Tests.Regime;

// Boundary tests for the signal bands. These bands are shown to users and
// documented in the UI (lib/indicators.ts) — changing them is a product
// decision, and this suite makes an accidental change loud.
public class SignalClassifierTests
{
    [Theory]
    [InlineData(4.01, "Restrictive")]
    [InlineData(4.00, "Neutral")]
    [InlineData(2.01, "Neutral")]
    [InlineData(2.00, "Accommodative")]
    [InlineData(0.25, "Accommodative")]
    public void FedFundsRate_Bands(decimal value, string expected)
    {
        Assert.Equal(expected, SignalClassifier.Classify(MacroIndicatorType.FedFundsRate, value));
    }

    [Theory]
    [InlineData(-0.01, "Inverted")]
    [InlineData(0.00, "Flat")]
    [InlineData(0.49, "Flat")]
    [InlineData(0.50, "Normal")]
    public void YieldCurveSpread_Bands(decimal value, string expected)
    {
        Assert.Equal(expected, SignalClassifier.Classify(MacroIndicatorType.YieldCurveSpread, value));
    }

    [Theory]
    [InlineData(30.01, "Panic")]
    [InlineData(30.00, "Elevated")]
    [InlineData(20.01, "Elevated")]
    [InlineData(20.00, "Neutral")]
    [InlineData(15.00, "Neutral")]
    [InlineData(14.99, "Complacent")]
    public void Vix_Bands(decimal value, string expected)
    {
        Assert.Equal(expected, SignalClassifier.Classify(MacroIndicatorType.VIX, value));
    }

    [Theory]
    [InlineData(0.50, "Recession Signal")]
    [InlineData(0.49, "Warning")]
    [InlineData(0.30, "Warning")]
    [InlineData(0.29, "No Signal")]
    public void SahmRule_Bands(decimal value, string expected)
    {
        Assert.Equal(expected, SignalClassifier.Classify(MacroIndicatorType.SahmRule, value));
    }

    [Theory]
    [InlineData(75.00, "Extreme Greed")]
    [InlineData(74.99, "Greed")]
    [InlineData(55.00, "Greed")]
    [InlineData(54.99, "Neutral")]
    [InlineData(45.00, "Neutral")]
    [InlineData(44.99, "Fear")]
    [InlineData(25.00, "Fear")]
    [InlineData(24.99, "Extreme Fear")]
    public void CryptoFearGreed_Bands(decimal value, string expected)
    {
        Assert.Equal(expected, SignalClassifier.Classify(MacroIndicatorType.CryptoFearGreed, value));
    }

    [Theory]
    [InlineData(-0.01, "Contracting")]
    [InlineData(0.00, "Slow")]
    [InlineData(1.99, "Slow")]
    [InlineData(2.00, "Expanding")]
    public void Gdp_Bands(decimal value, string expected)
    {
        Assert.Equal(expected, SignalClassifier.Classify(MacroIndicatorType.GDP, value));
    }

    [Theory]
    [InlineData(6.01, "Stressed")]
    [InlineData(6.00, "Elevated")]
    [InlineData(4.01, "Elevated")]
    [InlineData(4.00, "Calm")]
    public void HighYieldSpread_Bands(decimal value, string expected)
    {
        Assert.Equal(expected, SignalClassifier.Classify(MacroIndicatorType.HighYieldSpread, value));
    }
}
