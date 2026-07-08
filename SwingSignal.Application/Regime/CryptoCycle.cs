using SwingSignal.Domain.Entities;

namespace SwingSignal.Application.Regime;

/// Crypto-native cycle context that needs no external data: the Bitcoin
/// halving calendar (fixed dates) and the Mayer Multiple (price vs 200-day
/// average, computed from our own candles). Context only — with 3-4 halvings
/// in history these patterns are suggestive, never predictive.
public static class CryptoCycle
{
    public static readonly DateTime[] HalvingDates =
    [
        new(2012, 11, 28),
        new(2016, 7, 9),
        new(2020, 5, 11),
        new(2024, 4, 19),
    ];

    /// Whole calendar months since the most recent halving, or null before the first one.
    public static int? MonthsSinceHalving(DateTime asOf)
    {
        var last = HalvingDates.LastOrDefault(d => d <= asOf);
        if (last == default) return null;

        var months = (asOf.Year - last.Year) * 12 + asOf.Month - last.Month;
        if (asOf.Day < last.Day) months--;
        return months;
    }

    public static string? HalvingBullet(DateTime asOf)
    {
        var months = MonthsSinceHalving(asOf);
        if (months is null) return null;

        var phase = months switch
        {
            < 6 =>
                "early post-halving phase — historically a quiet accumulation period before major moves",
            < 18 =>
                "the historical markup window — in past cycles, most of Bitcoin's major rallies happened 6-18 months after a halving",
            < 30 =>
                "late-cycle territory — previous cycles saw their tops and corrections in this window",
            _ =>
                "pre-halving consolidation — historically a basing period ahead of the next supply cut",
        };

        return $"Halving cycle: Bitcoin is {months} months past its last halving, which puts it in {phase}. " +
               "Only a handful of halvings have ever happened, so treat this pattern as context, not destiny.";
    }

    /// Mayer Multiple = price / 200-day simple moving average.
    /// Thresholds (2.4 / 0.8) come from Bitcoin's own history.
    public static decimal? MayerMultiple(List<Candle> candles)
    {
        var raw = MayerMultipleAt(candles, candles.Count - 1);
        return raw is null ? null : Math.Round(raw.Value, 2);
    }

    /// Unrounded Mayer Multiple at a historical candle index (null if fewer
    /// than 200 candles precede it).
    public static decimal? MayerMultipleAt(List<Candle> candles, int index)
    {
        if (index < 199 || index >= candles.Count) return null;

        decimal sum = 0;
        for (var i = index - 199; i <= index; i++)
            sum += candles[i].Close;

        var sma = sum / 200;
        if (sma == 0) return null;

        return candles[index].Close / sma;
    }

    // Approximate halving cycle length. Actual gaps were 44-48 months; the
    // phase comparison is circular so the approximation only blurs, never wraps wrongly.
    public const double CycleLengthMonths = 48.0;

    /// Gaussian similarity factor in (0, 1] between two crypto-cycle positions.
    /// Two dimensions, each roughly on a 0..1 scale:
    ///  - halving phase, compared circularly (month 47 neighbors month 1);
    ///  - |ln| gap between Mayer Multiples (how stretched price was vs its 200-day avg).
    /// A missing dimension contributes zero — no information, no penalty.
    /// Smaller bandwidth = stricter down-weighting of different cycle positions.
    public static double CycleFactor(
        DateTime now, decimal? mayerNow,
        DateTime analog, decimal? mayerAnalog,
        double bandwidth)
    {
        var monthsNow = MonthsSinceHalving(now);
        var monthsAnalog = MonthsSinceHalving(analog);

        var dPhase = 0.0;
        if (monthsNow is not null && monthsAnalog is not null)
        {
            var fracNow = monthsNow.Value % CycleLengthMonths / CycleLengthMonths;
            var fracAnalog = monthsAnalog.Value % CycleLengthMonths / CycleLengthMonths;
            var diff = Math.Abs(fracNow - fracAnalog);
            dPhase = Math.Min(diff, 1.0 - diff) * 2.0; // 0 = same phase, 1 = opposite
        }

        var dMayer = 0.0;
        if (mayerNow is > 0 && mayerAnalog is > 0)
            dMayer = Math.Abs(Math.Log((double)mayerNow.Value) - Math.Log((double)mayerAnalog.Value));

        return Math.Exp(-(dPhase * dPhase + dMayer * dMayer) / (bandwidth * bandwidth));
    }

    public static string? MayerBullet(List<Candle> candles)
    {
        var mayer = MayerMultiple(candles);
        if (mayer is null) return null;

        var zone = mayer switch
        {
            > 2.4m =>
                "historically overheated territory — past readings this stretched marked major top zones",
            < 0.8m =>
                "historically depressed territory — in past cycles this zone marked major bottoms",
            _ =>
                "the historically normal range — neither stretched nor washed out",
        };

        return $"Mayer Multiple: price is {mayer:0.00}x its own 200-day average, {zone}.";
    }
}
