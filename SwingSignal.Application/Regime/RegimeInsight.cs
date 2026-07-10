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
