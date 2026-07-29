namespace RegimeDeck.Application.Regime;

/// The fixed catalog of canonical macro regimes. Each is a stylized-but-real
/// set of indicator signals — fed through the SAME RegimeInsight pipeline as
/// the live regime — so a regime's playbook is computed exactly like today's,
/// never a hand-written "this always works" table. Order is the cycle order
/// shown on the page (expansion → recovery → late cycle → stress).
public static class RegimePlaybookCatalog
{
    public sealed record RegimeArchetype(
        string Id,
        string Name,
        string Summary,
        IReadOnlyList<string> Hallmarks,
        IReadOnlyDictionary<string, string> Signals);

    public static IReadOnlyList<RegimeArchetype> All { get; } =
    [
        new("risk-on-expansion", "Risk-On Expansion",
            "Mid-cycle sweet spot: growth is solid, inflation is contained, policy is neutral and markets are calm.",
            ["Solid, broad growth", "Inflation near target", "Policy neutral, liquidity ample", "Low volatility, tight credit"],
            new Dictionary<string, string>
            {
                ["FedFundsRate"] = "Neutral", ["FedBalanceSheet"] = "Stable", ["M2MoneySupply"] = "Expanding",
                ["ReverseRepo"] = "Normal", ["RealYield10Y"] = "Moderate",
                ["TreasuryYield10Y"] = "Normal", ["TreasuryYield2Y"] = "Normal", ["TreasuryYield3M"] = "Normal",
                ["YieldCurveSpread"] = "Healthy", ["YieldSpread10Y3M"] = "Healthy",
                ["CPI"] = "Low", ["CorePCE"] = "On Target",
                ["GDP"] = "Expanding", ["UnemploymentRate"] = "Low", ["JoblessClaims"] = "Low",
                ["SahmRule"] = "No Signal", ["RetailSales"] = "Strong", ["HousingStarts"] = "Strong",
                ["ConsumerSentiment"] = "Optimistic",
                ["VIX"] = "Calm", ["HighYieldSpread"] = "Low", ["DollarIndex"] = "Weak USD",
                ["CryptoFearGreed"] = "Greed",
                ["GoldPrice"] = "Stable", ["OilWTI"] = "Strong", ["Copper"] = "Growth Signal",
            }),

        new("reflation-recovery", "Reflation & Recovery",
            "Early-cycle rebound: the Fed is easing hard, liquidity is flooding in and growth is turning up off a low base.",
            ["Aggressive easing / QE", "Liquidity expanding fast", "Growth recovering", "Stress fading, real yields negative"],
            new Dictionary<string, string>
            {
                ["FedFundsRate"] = "Accommodative", ["FedBalanceSheet"] = "QE (Expanding)", ["M2MoneySupply"] = "Expanding Fast",
                ["ReverseRepo"] = "Low", ["RealYield10Y"] = "Negative (Easy)",
                ["TreasuryYield10Y"] = "Low", ["TreasuryYield2Y"] = "Low", ["TreasuryYield3M"] = "Low",
                ["YieldCurveSpread"] = "Healthy", ["YieldSpread10Y3M"] = "Healthy",
                ["CPI"] = "Low", ["CorePCE"] = "On Target",
                ["GDP"] = "Slow", ["UnemploymentRate"] = "Elevated", ["JoblessClaims"] = "Low",
                ["SahmRule"] = "No Signal", ["RetailSales"] = "Strong", ["HousingStarts"] = "Strong",
                ["ConsumerSentiment"] = "Optimistic",
                ["VIX"] = "Neutral", ["HighYieldSpread"] = "Low", ["DollarIndex"] = "Weak USD",
                ["CryptoFearGreed"] = "Fear",
                ["GoldPrice"] = "Elevated", ["OilWTI"] = "Strong", ["Copper"] = "Growth Signal",
            }),

        new("overheating-late-cycle", "Overheating / Late Cycle",
            "Growth is still hot but inflation has forced the Fed to tighten — financial assets stretch while real assets and cash gain appeal.",
            ["Strong growth, tight labor", "Inflation running hot", "Policy tightening (QT)", "Curve flattening, complacent volatility"],
            new Dictionary<string, string>
            {
                ["FedFundsRate"] = "Restrictive", ["FedBalanceSheet"] = "QT (Contracting)", ["M2MoneySupply"] = "Contracting",
                ["ReverseRepo"] = "High", ["RealYield10Y"] = "High",
                ["TreasuryYield10Y"] = "High", ["TreasuryYield2Y"] = "High", ["TreasuryYield3M"] = "High",
                ["YieldCurveSpread"] = "Flat", ["YieldSpread10Y3M"] = "Flat",
                ["CPI"] = "Elevated", ["CorePCE"] = "Above Target",
                ["GDP"] = "Expanding", ["UnemploymentRate"] = "Low", ["JoblessClaims"] = "Low",
                ["SahmRule"] = "No Signal", ["RetailSales"] = "Strong", ["HousingStarts"] = "Slow",
                ["ConsumerSentiment"] = "Optimistic",
                ["VIX"] = "Complacent", ["HighYieldSpread"] = "Low", ["DollarIndex"] = "Strong USD",
                ["CryptoFearGreed"] = "Greed",
                ["GoldPrice"] = "Elevated", ["OilWTI"] = "High", ["Copper"] = "Growth Signal",
            }),

        new("stagflation", "Stagflation",
            "The worst mix: inflation stays hot while growth stalls, so the Fed can't ride to the rescue. Real assets shine; paper assets struggle.",
            ["Inflation hot and sticky", "Growth stalling", "Policy stuck tight", "Curve inverted, dollar strong"],
            new Dictionary<string, string>
            {
                ["FedFundsRate"] = "Restrictive", ["FedBalanceSheet"] = "QT (Contracting)", ["M2MoneySupply"] = "Contracting",
                ["ReverseRepo"] = "High", ["RealYield10Y"] = "High",
                ["TreasuryYield10Y"] = "High", ["TreasuryYield2Y"] = "High", ["TreasuryYield3M"] = "High",
                ["YieldCurveSpread"] = "Inverted", ["YieldSpread10Y3M"] = "Inverted",
                ["CPI"] = "Elevated", ["CorePCE"] = "Elevated",
                ["GDP"] = "Slow", ["UnemploymentRate"] = "Elevated", ["JoblessClaims"] = "Elevated",
                ["SahmRule"] = "Warning", ["RetailSales"] = "Flat", ["HousingStarts"] = "Falling",
                ["ConsumerSentiment"] = "Pessimistic",
                ["VIX"] = "Elevated", ["HighYieldSpread"] = "Elevated", ["DollarIndex"] = "Strong USD",
                ["CryptoFearGreed"] = "Fear",
                ["GoldPrice"] = "Strong", ["OilWTI"] = "High", ["Copper"] = "Contraction Signal",
            }),

        new("tightening-disinflation", "Tightening & Disinflation",
            "Policy is restrictive and inflation is finally cooling, but growth is slowing with it. You get paid to wait in short-term bills as rate cuts approach.",
            ["Restrictive policy, QT", "Inflation cooling", "Growth slowing, curve inverted", "No recession confirmation yet"],
            new Dictionary<string, string>
            {
                ["FedFundsRate"] = "Restrictive", ["FedBalanceSheet"] = "QT (Contracting)", ["M2MoneySupply"] = "Contracting",
                ["ReverseRepo"] = "High", ["RealYield10Y"] = "High",
                ["TreasuryYield10Y"] = "High", ["TreasuryYield2Y"] = "High", ["TreasuryYield3M"] = "High",
                ["YieldCurveSpread"] = "Inverted", ["YieldSpread10Y3M"] = "Inverted",
                ["CPI"] = "Low", ["CorePCE"] = "On Target",
                ["GDP"] = "Slow", ["UnemploymentRate"] = "Normal", ["JoblessClaims"] = "Normal",
                ["SahmRule"] = "No Signal", ["RetailSales"] = "Flat", ["HousingStarts"] = "Falling",
                ["ConsumerSentiment"] = "Pessimistic",
                ["VIX"] = "Neutral", ["HighYieldSpread"] = "Elevated", ["DollarIndex"] = "Strong USD",
                ["CryptoFearGreed"] = "Fear",
                ["GoldPrice"] = "Stable", ["OilWTI"] = "Falling", ["Copper"] = "Contraction Signal",
            }),

        new("risk-off-recession", "Risk-Off Recession",
            "Growth is contracting, the labor market has cracked and markets are in flight-to-safety mode. Capital preservation and duration lead.",
            ["Recession confirmed (Sahm)", "Panic volatility, credit stress", "Curve inverted, policy still tight", "Inflation collapsing"],
            new Dictionary<string, string>
            {
                ["FedFundsRate"] = "Restrictive", ["FedBalanceSheet"] = "QT (Contracting)", ["M2MoneySupply"] = "Contracting",
                ["ReverseRepo"] = "High", ["RealYield10Y"] = "High",
                ["TreasuryYield10Y"] = "High", ["TreasuryYield2Y"] = "High", ["TreasuryYield3M"] = "High",
                ["YieldCurveSpread"] = "Inverted", ["YieldSpread10Y3M"] = "Inverted",
                ["CPI"] = "Low", ["CorePCE"] = "On Target",
                ["GDP"] = "Contracting", ["UnemploymentRate"] = "Elevated", ["JoblessClaims"] = "Elevated",
                ["SahmRule"] = "Recession Signal", ["RetailSales"] = "Falling", ["HousingStarts"] = "Falling",
                ["ConsumerSentiment"] = "Pessimistic",
                ["VIX"] = "Panic", ["HighYieldSpread"] = "Stressed", ["DollarIndex"] = "Strong USD",
                ["CryptoFearGreed"] = "Extreme Fear",
                ["GoldPrice"] = "Strong", ["OilWTI"] = "Falling", ["Copper"] = "Contraction Signal",
            }),
    ];
}
