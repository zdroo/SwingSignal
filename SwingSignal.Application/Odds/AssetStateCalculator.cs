using SwingSignal.Domain.Entities;

namespace SwingSignal.Application.Odds;

// The asset's own technical state at a point in time. Used to CONDITION the
// macro analogs (prefer historical matches where the asset was in a similar
// state), never as a standalone buy/sell signal.
public record AssetState(double Ma200Pos, double High52Dist, double Rsi14);

public record AssetStateStds(double Ma200Pos, double High52Dist, double Rsi14)
{
    public bool IsUsable => Ma200Pos > 1e-6 && High52Dist > 1e-6 && Rsi14 > 1e-6;
}

public static class AssetStateCalculator
{
    private const int MaPeriod = 200;
    private const int HighWindow = 252;  // ~52 trading weeks
    private const int RsiPeriod = 14;

    // State at a candle index; null when there isn't enough history yet.
    public static AssetState? ComputeAt(List<Candle> candles, int index)
    {
        if (index < MaPeriod) return null;

        // Position vs 200-day SMA, in %
        decimal sum = 0;
        for (var i = index - MaPeriod + 1; i <= index; i++)
            sum += candles[i].Close;
        var sma = sum / MaPeriod;
        var ma200Pos = (double)((candles[index].Close - sma) / sma * 100);

        // Distance from 52-week high, in % (0 = at the high, negative below)
        var start = Math.Max(0, index - HighWindow + 1);
        var high = candles[index].Close;
        for (var i = start; i <= index; i++)
            if (candles[i].Close > high) high = candles[i].Close;
        var high52Dist = (double)((candles[index].Close - high) / high * 100);

        // RSI(14), Cutler's variant (simple averages — order-independent, no seed drift)
        double gains = 0, losses = 0;
        for (var i = index - RsiPeriod + 1; i <= index; i++)
        {
            var change = (double)(candles[i].Close - candles[i - 1].Close);
            if (change > 0) gains += change; else losses -= change;
        }
        var rsi = losses < 1e-12 ? 100.0 : 100.0 - 100.0 / (1.0 + gains / losses);

        return new AssetState(ma200Pos, high52Dist, rsi);
    }

    // Monthly-strided state samples across the asset's history, for normalization stats.
    public static List<(DateTime Date, AssetState State)> SampleMonthlyStates(List<Candle> candles)
    {
        const int stride = 21;
        var samples = new List<(DateTime, AssetState)>();

        for (var i = MaPeriod; i < candles.Count; i += stride)
        {
            var state = ComputeAt(candles, i);
            if (state is not null)
                samples.Add((candles[i].OpenTime, state));
        }

        return samples;
    }

    public static AssetStateStds ComputeStds(IEnumerable<AssetState> states)
    {
        var list = states.ToList();
        if (list.Count < 24) return new AssetStateStds(0, 0, 0);

        return new AssetStateStds(
            Std(list.Select(s => s.Ma200Pos)),
            Std(list.Select(s => s.High52Dist)),
            Std(list.Select(s => s.Rsi14)));
    }

    // RMS of z-scored feature differences between two states.
    public static double Distance(AssetState a, AssetState b, AssetStateStds stds)
    {
        var d1 = (a.Ma200Pos - b.Ma200Pos) / stds.Ma200Pos;
        var d2 = (a.High52Dist - b.High52Dist) / stds.High52Dist;
        var d3 = (a.Rsi14 - b.Rsi14) / stds.Rsi14;
        return Math.Sqrt((d1 * d1 + d2 * d2 + d3 * d3) / 3.0);
    }

    // Gaussian down-weighting of an analog whose asset state differs from today's.
    public static double StateFactor(AssetState current, AssetState analog, AssetStateStds stds, double bandwidth)
    {
        var distance = Distance(current, analog, stds);
        return Math.Exp(-Math.Pow(distance / bandwidth, 2));
    }

    private static double Std(IEnumerable<double> values)
    {
        var list = values.ToList();
        var mean = list.Average();
        return Math.Sqrt(list.Average(v => (v - mean) * (v - mean)));
    }
}
