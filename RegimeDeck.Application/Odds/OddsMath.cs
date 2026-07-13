namespace RegimeDeck.Application.Odds;

/// Shared odds arithmetic used by both the live odds service and the
/// backtester — one implementation so they can never diverge.
public static class OddsMath
{
    /// Kish effective sample size: (Σw)² / Σw². Equal weights give n;
    /// a few dominant weights give something much smaller — 15 analogs
    /// carried by 3 heavy ones are really ~5 independent votes.
    public static double EffectiveSampleSize(IReadOnlyList<double> weights)
    {
        var sum = 0.0;
        var sumSq = 0.0;
        foreach (var w in weights)
        {
            sum += w;
            sumSq += w * w;
        }

        return sumSq <= 0 ? 0 : sum * sum / sumSq;
    }

    /// Shrinks raw analog odds toward the base rate by evidence quantity:
    /// k = nEff / (nEff + prior). Plenty of effective analogs → trust them;
    /// a thin sample → stay near the base rate. The prior is "how many
    /// effective analogs it takes to earn 50% trust".
    public static double AdaptiveShrink(double rawOdds, double baseRate, double nEff, double prior)
    {
        var k = nEff / (nEff + prior);
        return baseRate + k * (rawOdds - baseRate);
    }
}
