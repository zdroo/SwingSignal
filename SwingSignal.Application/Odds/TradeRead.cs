using System.Globalization;
using SwingSignal.Contracts.Regime;

namespace SwingSignal.Application.Odds;

/// Distills the three horizon odds into one statistical read: is there a
/// regime edge worth acting on, at which horizon, and how much weight can
/// the evidence carry? Fixed rules, every contribution emits a reason —
/// the same standard for every asset, every day.
public static class TradeRead
{
    public const string LongBias = "Long bias";
    public const string NoEdge = "No edge";
    public const string StandAside = "Stand aside";

    // A regime edge below this many percentage points is treated as noise
    private const double EdgeThreshold = 5;
    private const double StrongEdge = 8;
    private const double MinActionableOdds = 55;
    private const int SolidSample = 25;
    private const int ModestSample = 15;

    private const string Note =
        "A fixed statistical read of the analog outcomes on this page — the same rules " +
        "for every asset, every day. It describes the historical tilt of conditions like " +
        "today's; it is not a forecast or personal trade advice.";

    private sealed record Horizon(int Days, string Label, OddsForPeriodDto Odds);

    public static TradeReadDto? Compute(
        OddsForPeriodDto oneMonth, OddsForPeriodDto threeMonths, OddsForPeriodDto sixMonths,
        int matchesUsed)
    {
        var horizons = new List<Horizon>
        {
            new(30, "1-month", oneMonth),
            new(90, "3-month", threeMonths),
            new(180, "6-month", sixMonths),
        }.Where(h => h.Odds.TotalCases > 0 && h.Odds.BaseRate is not null).ToList();

        if (horizons.Count == 0) return null;

        var best = horizons.MaxBy(h => h.Odds.Edge)!;
        var worst = horizons.MinBy(h => h.Odds.Edge)!;
        var tiltedUp = horizons.Count(h => h.Odds.Edge >= EdgeThreshold);
        var tiltedDown = horizons.Count(h => h.Odds.Edge <= -EdgeThreshold);

        string stance;
        Horizon key;
        if (best.Odds.Edge >= EdgeThreshold && best.Odds.PositiveOdds >= MinActionableOdds)
        {
            stance = LongBias;
            key = best;
        }
        else if (worst.Odds.Edge <= -EdgeThreshold && best.Odds.Edge < EdgeThreshold)
        {
            stance = StandAside;
            key = worst;
        }
        else
        {
            stance = NoEdge;
            key = best;
        }

        var reasons = new List<string> { EdgeSentence(stance, key) };

        if (stance != NoEdge)
            AddConsistency(reasons, stance, key, tiltedUp, tiltedDown, horizons.Count);

        reasons.Add(matchesUsed >= SolidSample
            ? $"based on {matchesUsed} similar historical periods — a solid sample"
            : matchesUsed >= ModestSample
            ? $"based on {matchesUsed} similar historical periods — a modest sample"
            : $"only {matchesUsed} analog periods — thin evidence, treat with caution");

        var span = key.Days == 30 ? "1 month" : key.Days == 90 ? "3 months" : "6 months";
        reasons.Add(
            $"median analog outcome {Signed(key.Odds.MedianReturn)}%, worst case " +
            $"{Num(key.Odds.WorstCase)}% over {span} — " +
            "outcomes anywhere in that range are normal");

        var strength = matchesUsed < ModestSample ? "Weak"
            : Math.Abs(key.Odds.Edge) >= StrongEdge && matchesUsed >= SolidSample ? "Strong"
            : Math.Abs(key.Odds.Edge) >= EdgeThreshold ? "Moderate"
            : "Weak";

        return new TradeReadDto(stance, key.Days, strength, reasons, Note);
    }

    private static string EdgeSentence(string stance, Horizon key)
    {
        var odds = Num(key.Odds.PositiveOdds);
        var baseRate = Num(key.Odds.BaseRate!.Value);
        var edge = SignedNum(key.Odds.Edge);

        return stance switch
        {
            LongBias =>
                $"conditions like today put {key.Label} odds at {odds}% vs a {baseRate}% " +
                $"base rate — a {edge} pt regime edge",
            StandAside =>
                $"conditions like today put {key.Label} odds at {odds}%, {edge} pts below " +
                $"the asset's normal {baseRate}% base rate",
            _ => key.Odds.Edge >= EdgeThreshold
                ? $"the regime adds {edge} pts at {key.Label}, but odds still sit near a " +
                  $"coin flip ({odds}%) — not enough to act on"
                : $"the regime shifts {key.Label} odds by only {edge} pts — statistically " +
                  "indistinguishable from any other day",
        };
    }

    private static void AddConsistency(
        List<string> reasons, string stance, Horizon key, int tiltedUp, int tiltedDown, int total)
    {
        if (stance == LongBias)
        {
            if (tiltedUp == total && total > 1)
                reasons.Add("the positive tilt holds at every horizon");
            else if (tiltedDown > 0)
                reasons.Add("horizons disagree on direction — treat the read with extra caution");
            else if (tiltedUp == 1 && total > 1)
                reasons.Add($"the edge is specific to the {key.Label} horizon — other windows show little");
        }
        else if (tiltedDown == total && total > 1)
        {
            reasons.Add("odds run below the base rate at every horizon");
        }
        else if (tiltedUp > 0)
        {
            reasons.Add("horizons disagree on direction — treat the read with extra caution");
        }
    }

    private static string Num(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);
    private static string Num(decimal v) => v.ToString("0.#", CultureInfo.InvariantCulture);
    private static string SignedNum(double v) => (v >= 0 ? "+" : "") + Num(v);
    private static string Signed(decimal v) => (v >= 0 ? "+" : "") + Num(v);
}
