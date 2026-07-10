using SwingSignal.Contracts.Regime;

namespace SwingSignal.Application.Regime;

/// Turns raw indicator signals into insight: how market-moving each reading
/// is (severity), whether it is supportive or hostile (tone), a descriptive
/// Market Health composite, and a plain-words narrative. Purely rule-based —
/// no AI, fully auditable, and deliberately NOT part of the odds engine.
public static class RegimeInsight
{
    /// Severity at or above this counts as "market-moving".
    public const int MarketMoverThreshold = 2;

    // ── Severity: 0 background · 1 notable · 2 market-moving · 3 extreme ──
    private static readonly Dictionary<string, int> Severity = new()
    {
        ["Panic"] = 3,
        ["Stressed"] = 3,
        ["Recession Signal"] = 3,
        ["Inverted"] = 3,
        ["Extreme Fear"] = 3,
        ["Extreme Greed"] = 3,

        ["Restrictive"] = 2,
        ["Accommodative"] = 2,
        ["QE (Expanding)"] = 2,
        ["QT (Contracting)"] = 2,
        ["Negative (Easy)"] = 2,
        ["Expanding Fast"] = 2,
        ["Elevated"] = 2,
        ["High"] = 2,
        ["Contracting"] = 2,
        ["Falling"] = 2,
        ["Warning"] = 2,
        ["Inflationary"] = 2,
        ["Deflationary"] = 2,
        ["Pessimistic"] = 2,
        ["Risk-off"] = 2,
        ["Strong USD"] = 2,
        ["Growth Signal"] = 2,
        ["Contraction Signal"] = 2,

        ["Flat"] = 1,
        ["Low"] = 1,
        ["Slow"] = 1,
        ["Above Target"] = 1,
        ["Complacent"] = 1,
        ["Greed"] = 1,
        ["Fear"] = 1,
        ["Optimistic"] = 1,
        ["Risk-on"] = 1,
        ["Weak USD"] = 1,
        ["Expanding"] = 1,
        ["Strong"] = 1,
        ["Healthy"] = 1,
    };

    public static int SeverityOf(string signal) => Severity.GetValueOrDefault(signal, 0);

    // ── Tone: is this reading supportive or hostile? ───────────────────────
    public const string Good = "good";
    public const string Neutral = "neutral";
    public const string Caution = "caution";
    public const string Bad = "bad";

    private static readonly Dictionary<string, string> Tone = new()
    {
        // supportive
        ["Accommodative"] = Good,
        ["Healthy"] = Good,
        ["Low"] = Good,
        ["Expanding"] = Good,
        ["Expanding Fast"] = Good,
        ["QE (Expanding)"] = Good,
        ["Negative (Easy)"] = Good,
        ["On Target"] = Good,
        ["Strong"] = Good,
        ["Calm"] = Good,
        ["No Signal"] = Good,
        ["Growth Signal"] = Good,
        ["Optimistic"] = Good,
        ["Risk-on"] = Good,
        ["Weak USD"] = Good,
        ["Extreme Fear"] = Good, // contrarian: historically a buy zone

        // hostile
        ["Restrictive"] = Bad,
        ["Inverted"] = Bad,
        ["Contracting"] = Bad,
        ["QT (Contracting)"] = Bad,
        ["Contraction Signal"] = Bad,
        ["Stressed"] = Bad,
        ["Recession Signal"] = Bad,
        ["Panic"] = Bad,
        ["Pessimistic"] = Bad,
        ["Falling"] = Bad,
        ["Risk-off"] = Bad,
        ["Strong USD"] = Bad,
        ["Extreme Greed"] = Bad,

        // caution
        ["Elevated"] = Caution,
        ["High"] = Caution,
        ["Flat"] = Caution,
        ["Warning"] = Caution,
        ["Above Target"] = Caution,
        ["Slow"] = Caution,
        ["Inflationary"] = Caution,
        ["Complacent"] = Caution,
        ["Greed"] = Caution,
        ["Fear"] = Caution,

        // neutral
        ["Neutral"] = Neutral,
        ["Normal"] = Neutral,
        ["Stable"] = Neutral,
        ["Moderate"] = Neutral,
        ["Deflationary"] = Neutral,
    };

    public static string ToneOf(string signal) => Tone.GetValueOrDefault(signal, Neutral);

    // ── Market health: tone scores → group averages → equal-weight overall ──
    private static readonly Dictionary<string, int> ToneScore = new()
    {
        [Good] = 100,
        [Neutral] = 55, // "nothing notable" is mildly healthy, not mid-crisis
        [Caution] = 30,
        [Bad] = 0,
    };

    private static readonly (string Name, string Question, string[] Keys)[] HealthGroups =
    [
        ("Policy & Liquidity", "Is money cheap and flowing, or expensive and draining?",
            ["FedFundsRate", "FedBalanceSheet", "M2MoneySupply", "ReverseRepo", "RealYield10Y"]),
        ("Rates & Yield Curve", "What does the bond market expect — growth or recession?",
            ["TreasuryYield10Y", "TreasuryYield2Y", "TreasuryYield3M", "YieldCurveSpread", "YieldSpread10Y3M"]),
        ("Inflation", "Is inflation forcing the Fed's hand?",
            ["CPI", "CorePCE"]),
        ("Growth & Labor", "Is the real economy expanding and employing?",
            ["GDP", "UnemploymentRate", "JoblessClaims", "SahmRule", "RetailSales", "HousingStarts", "ConsumerSentiment"]),
        ("Market Stress", "Are markets calm or bracing for trouble?",
            ["VIX", "HighYieldSpread", "DollarIndex", "CryptoFearGreed"]),
        ("Commodities", "What do raw materials say about demand and fear?",
            ["GoldPrice", "OilWTI", "Copper"]),
    ];

    public static string HealthLabel(int score) => score switch
    {
        >= 70 => "Supportive",
        >= 55 => "Steady",
        >= 40 => "Mixed",
        >= 25 => "Strained",
        _ => "Stressed",
    };

    public static MarketHealthDto ComputeMarketHealth(IReadOnlyDictionary<string, string> signals)
    {
        var groups = new List<HealthGroupDto>();

        foreach (var (name, question, keys) in HealthGroups)
        {
            var members = keys
                .Where(signals.ContainsKey)
                .Select(key => new HealthMemberDto(key, signals[key], ToneOf(signals[key])))
                .ToList();

            if (members.Count == 0) continue;

            var score = (int)Math.Round(
                members.Average(m => (double)ToneScore[m.Tone]), MidpointRounding.AwayFromZero);
            groups.Add(new HealthGroupDto(name, question, score, HealthLabel(score), members));
        }

        var overall = groups.Count == 0
            ? 0
            : (int)Math.Round(groups.Average(g => (double)g.Score), MidpointRounding.AwayFromZero);

        return new MarketHealthDto(overall, HealthLabel(overall), groups);
    }

    // ── Playbook: which asset class do conditions favor for NEW money? ─────
    // A fixed, textbook regime playbook: each class scores 0-100 from the
    // health group scores (weights encode textbook macro relationships, e.g.
    // liquidity drives risk assets, cooling inflation + weakening growth
    // drive bonds), then sharp event signals adjust it. Every contribution
    // carries a plain-words reason so the ranking is fully auditable.

    public const string Favored = "Favored";
    public const string NeutralFit = "Neutral";
    public const string Headwinds = "Headwinds";

    private sealed record Driver(double Weight, double Value, string Helps, string Hurts);

    public static string VerdictOf(int score) => score switch
    {
        >= 65 => Favored,
        >= 45 => NeutralFit,
        _ => Headwinds,
    };

    public static PlaybookDto ComputePlaybook(
        MarketHealthDto health, IReadOnlyDictionary<string, string> signals)
    {
        // A missing group counts as "nothing notable" — same convention as ToneScore
        double Group(string name) =>
            health.Groups.FirstOrDefault(g => g.Name == name)?.Score ?? 55;

        var policy = Group("Policy & Liquidity");   // 100 = money cheap and flowing
        var curve = Group("Rates & Yield Curve");    // 100 = healthy curve
        var inflation = Group("Inflation");          // 100 = tame
        var growth = Group("Growth & Labor");        // 100 = expanding
        var stress = Group("Market Stress");         // 100 = calm

        string? Sig(string key) => signals.TryGetValue(key, out var v) ? v : null;

        var sahmTriggered = Sig("SahmRule") == "Recession Signal";
        var inverted = Sig("YieldSpread10Y3M") == "Inverted" || Sig("YieldCurveSpread") == "Inverted";
        var qe = Sig("FedBalanceSheet") == "QE (Expanding)";
        var qt = Sig("FedBalanceSheet") == "QT (Contracting)";
        var panic = Sig("VIX") == "Panic" || Sig("HighYieldSpread") == "Stressed";
        var cryptoSentiment = Sig("CryptoFearGreed");

        var ranked = new List<PlaybookAssetDto>();

        void Add(string name, Driver[] drivers, params (double Delta, string? Reason)[] events)
        {
            var score = drivers.Sum(d => d.Weight * d.Value);
            var reasons = new List<string>();

            // Only drivers that are actually pulling get a reason — mid-range
            // readings aren't worth a sentence
            foreach (var d in drivers.OrderByDescending(d => d.Weight))
            {
                if (d.Value >= 65) reasons.Add(d.Helps);
                else if (d.Value <= 35) reasons.Add(d.Hurts);
            }

            foreach (var (delta, reason) in events)
            {
                score += delta;
                if (reason is not null) reasons.Add(reason);
            }

            var rounded = (int)Math.Round(Math.Clamp(score, 0, 100), MidpointRounding.AwayFromZero);
            ranked.Add(new PlaybookAssetDto(name, rounded, VerdictOf(rounded), reasons));
        }

        Add("Stocks",
        [
            new(0.30, growth,
                "the real economy is expanding — an earnings tailwind",
                "growth and labor are deteriorating — an earnings risk"),
            new(0.25, policy,
                "money is cheap and flowing, which supports valuations",
                "money is expensive and draining, which pressures valuations"),
            new(0.25, stress,
                "markets are calm, so risk-taking is being rewarded",
                "markets are stressed, and risk assets get sold first"),
            new(0.20, inflation,
                "inflation is tame, so the Fed isn't forced to tighten",
                "inflation is hot, which keeps policy tight"),
        ],
            (sahmTriggered ? -10 : 0, sahmTriggered
                ? "the Sahm Rule has triggered — recessions are hostile to earnings" : null),
            (qe ? 5 : qt ? -5 : 0, qe
                ? "an expanding Fed balance sheet historically lifts equities"
                : qt ? "a shrinking Fed balance sheet is a persistent drag on equities" : null),
            (0, panic
                ? "note: panic-level stress has historically marked better multi-month entries than exits" : null));

        Add("Crypto (majors)",
        [
            new(0.40, policy,
                "liquidity is expanding — historically crypto's strongest tailwind",
                "liquidity is draining — historically crypto's strongest headwind"),
            new(0.30, stress,
                "risk appetite is healthy, which crypto amplifies",
                "risk-off stress hits crypto hardest of all"),
            new(0.15, growth,
                "a growing economy supports speculative demand",
                "a weakening economy drains speculative demand"),
            new(0.15, inflation,
                "tame inflation keeps real yields from crushing long-duration bets",
                "hot inflation pressures all long-duration bets, crypto included"),
        ],
            (sahmTriggered ? -10 : 0, sahmTriggered
                ? "a recession confirmation is hostile to speculative assets" : null),
            (qe ? 8 : qt ? -8 : 0, qe
                ? "fresh Fed liquidity historically reaches crypto first"
                : qt ? "the Fed's liquidity drain hits crypto first" : null),
            (cryptoSentiment == "Extreme Greed" ? -8 : cryptoSentiment == "Extreme Fear" ? 5 : 0,
                cryptoSentiment == "Extreme Greed"
                    ? "crowd euphoria (Extreme Greed) has historically preceded pullbacks"
                    : cryptoSentiment == "Extreme Fear"
                    ? "crowd capitulation (Extreme Fear) has historically been an entry, not an exit" : null));

        Add("Gold",
        [
            new(0.40, 100 - stress,
                "market stress is driving a flight to safety",
                "calm markets leave gold without a safety bid"),
            new(0.35, 100 - inflation,
                "hot inflation strengthens the classic hedge case",
                "tame inflation weakens the hedge case"),
            new(0.25, policy,
                "easy policy depresses real yields — gold's main fuel",
                "tight policy props up real yields — gold's main drag"),
        ]);

        Add("Long-Term Bonds",
        [
            new(0.35, inflation,
                "cooling inflation is the best backdrop for fixed coupons",
                "hot inflation erodes fixed coupons"),
            new(0.30, 100 - growth,
                "a weakening economy pulls rate cuts closer, lifting bond prices",
                "a strong economy keeps yields pinned high"),
            new(0.20, 100 - stress,
                "flight-to-quality flows favor Treasuries",
                "risk appetite pulls money away from safe assets"),
            new(0.15, policy,
                "an easing Fed lifts bond prices",
                "a tightening Fed pressures bond prices"),
        ],
            (sahmTriggered ? 10 : 0, sahmTriggered
                ? "recessions historically bring rate cuts, the strongest driver of bond rallies" : null));

        Add("Cash & T-Bills",
        [
            new(0.50, 100 - curve,
                "the yield curve is strained — short-term bills out-yield most alternatives",
                "a healthy yield curve means bills yield less than longer bonds"),
            new(0.50, 100 - stress,
                "in stressed markets cash preserves optionality for better entries",
                "in calm markets cash lags every risk asset"),
        ],
            (inverted ? 10 : 0, inverted
                ? "the curve is inverted — you are paid to wait in short-term bills" : null),
            (sahmTriggered ? 5 : 0, sahmTriggered
                ? "recession risk raises the value of staying liquid" : null));

        // Stable sort: ties resolve in definition order
        var ordered = ranked.OrderByDescending(a => a.Score).ToList();

        return new PlaybookDto(
            $"Current conditions favor {ordered[0].Name} for new money.",
            "A fixed, textbook playbook applied to today's readings — the same rules every day. " +
            "It describes what similar conditions have historically favored, not what will happen. " +
            "Not financial advice, and it plays no role in the odds engine.",
            ordered);
    }

    // ── Narrative: a few plain-words sentences on the overall picture ──────
    public static List<string> Summarize(IReadOnlyDictionary<string, MacroIndicatorValueDto> indicators)
    {
        var sentences = new List<string>();

        string? Sig(string key) => indicators.TryGetValue(key, out var v) ? v.Signal : null;
        decimal? Val(string key) => indicators.TryGetValue(key, out var v) ? v.Value : null;

        // 1. Monetary policy & liquidity
        var fed = Sig("FedFundsRate");
        var bs = Sig("FedBalanceSheet");
        if (fed is not null)
        {
            var rate = Val("FedFundsRate")?.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            var policy = fed switch
            {
                "Restrictive" => $"The Fed is restrictive (rates at {rate}%), deliberately cooling the economy",
                "Accommodative" => $"The Fed is accommodative (rates at {rate}%), actively supporting the economy",
                _ => $"The Fed is roughly neutral (rates at {rate}%), neither stimulating nor restraining",
            };
            var liquidity = bs switch
            {
                "QE (Expanding)" => ", while its balance sheet expands — liquidity is flowing into markets.",
                "QT (Contracting)" => ", while its balance sheet shrinks — liquidity is being drained.",
                not null => ", with a stable balance sheet — no liquidity push in either direction.",
                null => ".",
            };
            sentences.Add(policy + liquidity);
        }

        // 2. Inflation
        var cpi = Sig("CPI");
        var pce = Sig("CorePCE");
        if (cpi is not null || pce is not null)
        {
            var cpiVal = Val("CPI")?.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            if (cpi == "Elevated" || pce == "Elevated")
                sentences.Add($"Inflation is running hot (CPI {cpiVal}% YoY) — the main constraint keeping policy tight.");
            else if (cpi == "Low" && pce != "Above Target")
                sentences.Add($"Inflation is tame (CPI {cpiVal}% YoY), giving the Fed room to ease if the economy weakens.");
            else
                sentences.Add($"Inflation sits near target (CPI {cpiVal}% YoY) — not forcing the Fed's hand in either direction.");
        }

        // 3. Growth & labor
        var gdp = Sig("GDP");
        var unemp = Sig("UnemploymentRate");
        var sahm = Sig("SahmRule");
        var claims = Sig("JoblessClaims");
        if (sahm == "Recession Signal")
            sentences.Add("The labor market has triggered the Sahm Rule — historically a reliable real-time recession confirmation.");
        else if (sahm == "Warning" || claims == "Elevated" || unemp == "Elevated")
            sentences.Add("The labor market is showing cracks — job losses are creeping up, which historically snowballs once it starts.");
        else if (gdp == "Contracting")
            sentences.Add("Growth has turned negative even though the labor market holds — a fragile combination.");
        else if (gdp == "Slow")
            sentences.Add("Growth is positive but sluggish, with the labor market still holding up.");
        else if (gdp is not null || unemp is not null)
            sentences.Add("Growth and the labor market look solid — no recession signal from the real economy.");

        // 4. Market stress & warnings
        var curve = Sig("YieldSpread10Y3M") ?? Sig("YieldCurveSpread");
        var vix = Sig("VIX");
        var hy = Sig("HighYieldSpread");
        var stressBits = new List<string>();
        if (curve == "Inverted") stressBits.Add("the yield curve is inverted (a classic recession warning)");
        if (hy == "Stressed") stressBits.Add("credit markets are stressed");
        else if (hy == "Elevated") stressBits.Add("credit spreads are widening");
        if (vix == "Panic") stressBits.Add("equity volatility is in panic territory");
        else if (vix == "Elevated") stressBits.Add("equity markets are nervous");
        else if (vix == "Complacent") stressBits.Add("volatility is unusually low — markets may be complacent");

        if (stressBits.Count > 0)
            sentences.Add($"Warning signs: {string.Join("; ", stressBits)}.");
        else if (curve is not null || vix is not null || hy is not null)
            sentences.Add("Market stress gauges are quiet — credit is calm and volatility is contained.");

        // 5. Overall tone from the severity distribution
        var all = indicators.Values.ToList();
        var movers = all.Count(i => SeverityOf(i.Signal) >= MarketMoverThreshold);
        var extremes = all.Count(i => SeverityOf(i.Signal) >= 3);

        var tone = extremes >= 3
            ? "a high-tension macro backdrop — several indicators are at extremes, so expect regime-driven markets"
            : movers >= 6
            ? "a mixed, transitional backdrop — enough indicators are flashing to matter, without a clear crisis signal"
            : "a relatively calm macro backdrop — most indicators sit in normal ranges";

        sentences.Add($"Overall: {movers} of {all.Count} indicators are currently market-moving, suggesting {tone}.");

        return sentences;
    }
}
