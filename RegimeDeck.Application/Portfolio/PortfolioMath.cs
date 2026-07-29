using RegimeDeck.Application.Liquidity;
using RegimeDeck.Domain.Enums;

namespace RegimeDeck.Application.Portfolio;

/// Pure portfolio arithmetic: weights, concentration, classification, weighted
/// distribution stats, realized volatility and liquidity correlation. Kept
/// separate from the service so the maths is unit-tested in isolation.
public static class PortfolioMath
{
    private const int YoyWeeks = 52;
    private const int MinCorrelationSamples = 24;

    // Symbol-level overrides on top of MarketType: gold and bonds read as
    // defensive even though they arrive as commodity / equity tickers.
    private static readonly HashSet<string> GoldSymbols =
        new(StringComparer.OrdinalIgnoreCase) { "GC=F", "GLD", "IAU", "SGOL", "XAUUSD", "XAUUSD=X", "PHYS" };

    private static readonly HashSet<string> BondSymbols =
        new(StringComparer.OrdinalIgnoreCase)
        { "TLT", "IEF", "IEI", "SHY", "AGG", "BND", "GOVT", "TLH", "ZROZ", "EDV", "LQD", "VGLT", "BNDX" };

    /// Fractional weights (summing to 1) from dollar values.
    public static double[] NormalizeWeights(IReadOnlyList<decimal> values)
    {
        var total = values.Sum();
        if (total <= 0m) return [.. values.Select(_ => 0.0)];
        return [.. values.Select(v => (double)(v / total))];
    }

    /// Herfindahl index over percentage weights (0–10000). 10000 = one holding.
    public static int Hhi(IReadOnlyList<double> weightFractions) =>
        (int)Math.Round(weightFractions.Sum(w => Math.Pow(w * 100, 2)));

    public static string ConcentrationLabel(int hhi) =>
        hhi >= 5000 ? "Concentrated" : hhi >= 2500 ? "Moderate" : "Diversified";

    public static (string Class, bool RiskOn) Classify(string symbol, MarketType market)
    {
        if (GoldSymbols.Contains(symbol)) return ("Gold", false);
        if (BondSymbols.Contains(symbol)) return ("Bonds", false);

        return market switch
        {
            MarketType.Crypto => ("Crypto", true),
            MarketType.Index or MarketType.Stock => ("Stocks", true),
            MarketType.Commodity => ("Commodity", true),  // oil / copper — pro-cyclical
            MarketType.Forex => ("Forex", false),
            _ => ("Stocks", true),
        };
    }

    public static string Posture(bool riskOn) => riskOn ? "Risk-on" : "Defensive";

    // Same bands as the single-asset ConfidenceRisk card — a small, overlapping
    // sample never earns "high".
    public static string Confidence(int analogs) =>
        analogs < 15 ? "Very low" : analogs < 30 ? "Low" : "Modest";

    public static string LiquidityLabel(int beta) =>
        beta >= 40 ? "Liquidity-driven" : beta >= 15 ? "Mixed" : "Liquidity-insulated";

    /// Value at which cumulative weight crosses the percentile. Input ascending by value.
    public static decimal WeightedPercentile(
        IReadOnlyList<(decimal Value, double Weight)> sorted, double totalWeight, double percentile)
    {
        var threshold = totalWeight * percentile;
        var cumulative = 0.0;
        foreach (var (value, weight) in sorted)
        {
            cumulative += weight;
            if (cumulative >= threshold) return value;
        }
        return sorted[^1].Value;
    }

    public static double WeightedPositiveOdds(IReadOnlyList<(decimal Value, double Weight)> samples)
    {
        var total = samples.Sum(s => s.Weight);
        if (total <= 0) return 0;
        return samples.Where(s => s.Value > 0).Sum(s => s.Weight) / total * 100;
    }

    /// Annualized realized volatility (%) from a close series (daily). Null when
    /// there isn't enough history.
    public static double? AnnualizedVolatilityPct(IReadOnlyList<decimal> closes)
    {
        if (closes.Count < 20) return null;

        var returns = new List<double>(closes.Count - 1);
        for (var i = 1; i < closes.Count; i++)
            if (closes[i - 1] != 0m)
                returns.Add((double)((closes[i] - closes[i - 1]) / closes[i - 1]));

        if (returns.Count < 20) return null;

        var mean = returns.Average();
        var variance = returns.Sum(r => (r - mean) * (r - mean)) / (returns.Count - 1);
        return Math.Sqrt(variance) * Math.Sqrt(252) * 100;
    }

    /// Correlation (×100) of an asset's YoY return with global-liquidity YoY
    /// growth, over aligned weekly series. Null when too few overlapping samples.
    public static int? LiquidityCorrelation(
        IReadOnlyList<decimal> assetWeekly, IReadOnlyList<decimal> liquidityWeekly)
    {
        var n = Math.Min(assetWeekly.Count, liquidityWeekly.Count);
        var xs = new List<double>();
        var ys = new List<double>();
        for (var i = YoyWeeks; i < n; i++)
        {
            if (assetWeekly[i - YoyWeeks] == 0m || liquidityWeekly[i - YoyWeeks] == 0m) continue;
            xs.Add((double)(liquidityWeekly[i] / liquidityWeekly[i - YoyWeeks] - 1m));
            ys.Add((double)(assetWeekly[i] / assetWeekly[i - YoyWeeks] - 1m));
        }

        if (xs.Count < MinCorrelationSamples) return null;
        return (int)Math.Round(LiquidityMath.Correlation(xs, ys) * 100);
    }
}
