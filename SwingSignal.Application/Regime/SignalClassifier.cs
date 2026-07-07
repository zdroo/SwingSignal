using SwingSignal.Domain.Enums;

namespace SwingSignal.Application.Regime;

/// Maps an indicator's current value to a human-readable signal label.
/// YoY-transformed indicators receive the YoY % value, not the raw level.
public static class SignalClassifier
{
    public static string Classify(MacroIndicatorType type, decimal value) => type switch
    {
        MacroIndicatorType.FedFundsRate      => value > 4m ? "Restrictive" : value > 2m ? "Neutral" : "Accommodative",
        MacroIndicatorType.UnemploymentRate  => value > 6m ? "Elevated" : value > 4m ? "Neutral" : "Healthy",
        MacroIndicatorType.CPI               => value > 4m ? "Elevated" : value > 2m ? "Neutral" : "Low",           // YoY %
        MacroIndicatorType.GDP               => value < 0m ? "Contracting" : value < 2m ? "Slow" : "Expanding",     // YoY %
        MacroIndicatorType.GoldPrice         => value > 15m ? "Risk-off" : value < 0m ? "Risk-on" : "Neutral",      // YoY %
        MacroIndicatorType.OilWTI            => value > 30m ? "Inflationary" : value < -20m ? "Deflationary" : "Neutral", // YoY %
        MacroIndicatorType.TreasuryYield10Y  => value > 4m ? "High" : value > 2m ? "Neutral" : "Low",
        MacroIndicatorType.TreasuryYield2Y   => value > 4m ? "High" : value > 2m ? "Neutral" : "Low",
        MacroIndicatorType.TreasuryYield3M   => value > 4m ? "High" : value > 2m ? "Neutral" : "Low",
        MacroIndicatorType.YieldCurveSpread  => value < 0m ? "Inverted" : value < 0.5m ? "Flat" : "Normal",
        MacroIndicatorType.YieldSpread10Y3M  => value < 0m ? "Inverted" : value < 0.5m ? "Flat" : "Normal",
        MacroIndicatorType.FedBalanceSheet   => value > 5m ? "QE (Expanding)" : value < -2m ? "QT (Contracting)" : "Stable", // YoY %
        MacroIndicatorType.ReverseRepo       => value > 500m ? "High" : value > 100m ? "Moderate" : "Low",          // $ billions
        MacroIndicatorType.RealYield10Y      => value > 1.5m ? "Restrictive" : value < 0m ? "Negative (Easy)" : "Neutral",
        MacroIndicatorType.M2MoneySupply     => value > 10m ? "Expanding Fast" : value < 0m ? "Contracting" : "Normal", // YoY %
        MacroIndicatorType.CorePCE           => value > 3m ? "Elevated" : value > 2m ? "Above Target" : "On Target",    // YoY %
        MacroIndicatorType.JoblessClaims     => value > 350_000m ? "Elevated" : value > 250_000m ? "Normal" : "Strong",
        MacroIndicatorType.ConsumerSentiment => value < 70m ? "Pessimistic" : value > 90m ? "Optimistic" : "Neutral",
        MacroIndicatorType.RetailSales       => value < 0m ? "Contracting" : value > 5m ? "Strong" : "Normal",      // YoY %
        MacroIndicatorType.HousingStarts     => value < -10m ? "Falling" : value > 10m ? "Expanding" : "Stable",    // YoY %
        MacroIndicatorType.HighYieldSpread   => value > 6m ? "Stressed" : value > 4m ? "Elevated" : "Calm",
        MacroIndicatorType.SahmRule          => value >= 0.5m ? "Recession Signal" : value >= 0.3m ? "Warning" : "No Signal",
        MacroIndicatorType.VIX               => value > 30m ? "Panic" : value > 20m ? "Elevated" : value >= 15m ? "Neutral" : "Complacent",
        MacroIndicatorType.DollarIndex       => value > 105m ? "Strong USD" : value < 95m ? "Weak USD" : "Neutral",
        MacroIndicatorType.Copper            => value > 15m ? "Growth Signal" : value < -15m ? "Contraction Signal" : "Neutral", // YoY %
        MacroIndicatorType.CryptoFearGreed   => value >= 75m ? "Extreme Greed" : value >= 55m ? "Greed" : value >= 45m ? "Neutral" : value >= 25m ? "Fear" : "Extreme Fear",
        _                                    => "Neutral"
    };
}
