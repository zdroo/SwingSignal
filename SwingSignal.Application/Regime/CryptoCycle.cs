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
        if (candles.Count < 200) return null;

        decimal sum = 0;
        for (var i = candles.Count - 200; i < candles.Count; i++)
            sum += candles[i].Close;

        var sma = sum / 200;
        if (sma == 0) return null;

        return Math.Round(candles[^1].Close / sma, 2);
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
