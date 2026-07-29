namespace RegimeDeck.Application.Liquidity;

/// Pure liquidity arithmetic: unit-normalized levels (every source series comes
/// in a different unit), forward-fill alignment, correlation and change. Kept
/// separate from the service so the fiddly unit maths is unit-tested in isolation.
public static class LiquidityMath
{
    // ── Levels, all normalized to USD billions ──────────────────────────────
    // WALCL (Fed) and WTREGEN (TGA) are Millions USD; RRP is already Billions USD.
    public static decimal FedNetLiquidityBil(decimal walclMil, decimal tgaMil, decimal rrpBil) =>
        walclMil / 1000m - tgaMil / 1000m - rrpBil;

    public static decimal FedBil(decimal walclMil) => walclMil / 1000m;

    // ECB assets are Millions EUR; eurUsd is USD per 1 EUR.
    public static decimal EcbBil(decimal ecbMil, decimal eurUsd) => ecbMil * eurUsd / 1000m;

    // BoJ assets are in units of 100 Million Yen; jpyUsd is Yen per 1 USD.
    // value × 1e8 yen ÷ jpyUsd ÷ 1e9 (yen→USD→billions) = value × 0.1 / jpyUsd.
    public static decimal BojBil(decimal bojHundredMilYen, decimal jpyUsd) =>
        jpyUsd == 0m ? 0m : bojHundredMilYen * 0.1m / jpyUsd;

    // ── Forward fill: the latest value at or before `asOf` (series ascending) ─
    public static decimal? AsOf(IReadOnlyList<(DateTime Date, decimal Value)> series, DateTime asOf)
    {
        decimal? last = null;
        foreach (var (date, value) in series)
        {
            if (date > asOf) break;
            last = value;
        }
        return last;
    }

    // ── % change between the last value and `back` points earlier ────────────
    public static double ChangePct(IReadOnlyList<decimal> values, int back)
    {
        if (values.Count <= back) return 0;
        var now = values[^1];
        var then = values[^(back + 1)];
        if (then == 0m) return 0;
        return (double)((now - then) / then) * 100.0;
    }

    public static string Trend(double changePct) =>
        changePct > 0.5 ? "Expanding" : changePct < -0.5 ? "Contracting" : "Flat";

    // ── Pearson correlation over paired samples ──────────────────────────────
    public static double Correlation(IReadOnlyList<double> xs, IReadOnlyList<double> ys)
    {
        var n = Math.Min(xs.Count, ys.Count);
        if (n < 3) return 0;

        double meanX = 0, meanY = 0;
        for (var i = 0; i < n; i++) { meanX += xs[i]; meanY += ys[i]; }
        meanX /= n; meanY /= n;

        double covar = 0, varX = 0, varY = 0;
        for (var i = 0; i < n; i++)
        {
            var dx = xs[i] - meanX;
            var dy = ys[i] - meanY;
            covar += dx * dy;
            varX += dx * dx;
            varY += dy * dy;
        }

        var denom = Math.Sqrt(varX * varY);
        return denom == 0 ? 0 : Math.Clamp(covar / denom, -1.0, 1.0);
    }
}
