using SwingSignal.Application.Regime;
using SwingSignal.Contracts.Regime;

namespace SwingSignal.Tests.Regime;

// The Market Health arithmetic is a user-facing promise ("supportive=100,
// neutral=55, caution=30, hostile=0, averaged within groups then across
// groups") — these tests pin it exactly, including both extremes.
public class RegimeInsightTests
{
    private static readonly string[] AllKeys =
    [
        "FedFundsRate", "FedBalanceSheet", "M2MoneySupply", "ReverseRepo", "RealYield10Y",
        "TreasuryYield10Y", "TreasuryYield2Y", "TreasuryYield3M", "YieldCurveSpread", "YieldSpread10Y3M",
        "CPI", "CorePCE",
        "GDP", "UnemploymentRate", "JoblessClaims", "SahmRule", "RetailSales", "HousingStarts", "ConsumerSentiment",
        "VIX", "HighYieldSpread", "DollarIndex", "CryptoFearGreed",
        "GoldPrice", "OilWTI", "Copper",
    ];

    private static Dictionary<string, string> AllWith(string signal) =>
        AllKeys.ToDictionary(k => k, _ => signal);

    // ── Tone ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Healthy", "good")]
    [InlineData("Panic", "bad")]
    [InlineData("Elevated", "caution")]
    [InlineData("Neutral", "neutral")]
    [InlineData("Extreme Fear", "good")]   // contrarian: historically a buy zone
    [InlineData("Extreme Greed", "bad")]
    [InlineData("Some Future Signal", "neutral")] // unknown → neutral fallback
    public void ToneOf_MapsDirectionsExactly(string signal, string tone)
    {
        Assert.Equal(tone, RegimeInsight.ToneOf(signal));
    }

    // ── Label band edges ──────────────────────────────────────────────────

    [Theory]
    [InlineData(100, "Supportive")]
    [InlineData(70, "Supportive")]
    [InlineData(69, "Steady")]
    [InlineData(55, "Steady")]
    [InlineData(54, "Mixed")]
    [InlineData(40, "Mixed")]
    [InlineData(39, "Strained")]
    [InlineData(25, "Strained")]
    [InlineData(24, "Stressed")]
    [InlineData(0, "Stressed")]
    public void HealthLabel_BandEdges(int score, string label)
    {
        Assert.Equal(label, RegimeInsight.HealthLabel(score));
    }

    // ── Extremes ──────────────────────────────────────────────────────────

    [Fact]
    public void EveryReadingSupportive_Exactly100()
    {
        var health = RegimeInsight.ComputeMarketHealth(AllWith("Healthy"));

        Assert.Equal(100, health.Score);
        Assert.Equal("Supportive", health.Label);
        Assert.Equal(6, health.Groups.Count);
        Assert.All(health.Groups, g =>
        {
            Assert.Equal(100, g.Score);
            Assert.Equal("Supportive", g.Label);
        });
    }

    [Fact]
    public void EveryReadingHostile_Exactly0()
    {
        var health = RegimeInsight.ComputeMarketHealth(AllWith("Panic"));

        Assert.Equal(0, health.Score);
        Assert.Equal("Stressed", health.Label);
        Assert.All(health.Groups, g => Assert.Equal(0, g.Score));
    }

    [Fact]
    public void EveryReadingNeutral_Exactly55_Steady()
    {
        var health = RegimeInsight.ComputeMarketHealth(AllWith("Neutral"));

        Assert.Equal(55, health.Score);
        Assert.Equal("Steady", health.Label);
    }

    [Fact]
    public void NoIndicators_ScoreZeroNoGroups()
    {
        var health = RegimeInsight.ComputeMarketHealth(new Dictionary<string, string>());

        Assert.Equal(0, health.Score);
        Assert.Empty(health.Groups);
    }

    // ── Group arithmetic ──────────────────────────────────────────────────

    [Fact]
    public void WithinGroup_GoodPlusCaution_Averages65()
    {
        var health = RegimeInsight.ComputeMarketHealth(new Dictionary<string, string>
        {
            ["CPI"] = "Healthy",      // good  → 100
            ["CorePCE"] = "Elevated", // caution → 30
        });

        var group = Assert.Single(health.Groups);
        Assert.Equal("Inflation", group.Name);
        Assert.Equal(65, group.Score);
        Assert.Equal("Steady", group.Label);
        Assert.Equal(65, health.Score);
    }

    [Fact]
    public void AcrossGroups_100And0_Average50Mixed()
    {
        var health = RegimeInsight.ComputeMarketHealth(new Dictionary<string, string>
        {
            ["CPI"] = "Healthy",
            ["CorePCE"] = "Healthy",   // Inflation: 100
            ["GoldPrice"] = "Panic",   // Commodities: 0
        });

        Assert.Equal([100, 0], health.Groups.Select(g => g.Score));
        Assert.Equal(50, health.Score);
        Assert.Equal("Mixed", health.Label);
    }

    [Fact]
    public void GroupSizeDoesNotChangeItsVote()
    {
        var health = RegimeInsight.ComputeMarketHealth(new Dictionary<string, string>
        {
            // Growth & Labor — all seven members hostile → 0
            ["GDP"] = "Contracting",
            ["UnemploymentRate"] = "Contracting",
            ["JoblessClaims"] = "Contracting",
            ["SahmRule"] = "Recession Signal",
            ["RetailSales"] = "Contracting",
            ["HousingStarts"] = "Falling",
            ["ConsumerSentiment"] = "Pessimistic",
            // Commodities — one member, supportive → 100
            ["Copper"] = "Growth Signal",
        });

        Assert.Equal([0, 100], health.Groups.Select(g => g.Score));
        Assert.Equal(50, health.Score);
    }

    [Fact]
    public void OnlyPresentIndicatorsBecomeMembers()
    {
        var health = RegimeInsight.ComputeMarketHealth(new Dictionary<string, string>
        {
            ["VIX"] = "Calm",
            ["HighYieldSpread"] = "Stressed",
        });

        var group = Assert.Single(health.Groups);
        Assert.Equal("Market Stress", group.Name);
        Assert.Equal(["VIX", "HighYieldSpread"], group.Members.Select(m => m.Key));
        Assert.Equal(50, group.Score); // (100 + 0) / 2
    }

    [Fact]
    public void UnevenAverages_RoundHalfAwayFromZero()
    {
        var health = RegimeInsight.ComputeMarketHealth(new Dictionary<string, string>
        {
            ["CPI"] = "Healthy",     // 100
            ["CorePCE"] = "Neutral", // 55
        });

        Assert.Equal(78, health.Groups[0].Score); // 77.5 → 78
    }

    // ── Severity / market movers ──────────────────────────────────────────

    [Theory]
    [InlineData("Panic", 3)]
    [InlineData("Restrictive", 2)]
    [InlineData("Flat", 1)]
    [InlineData("Neutral", 0)]
    [InlineData("Unknown Signal", 0)]
    public void SeverityOf_MapsExactly(string signal, int severity)
    {
        Assert.Equal(severity, RegimeInsight.SeverityOf(signal));
    }

    // ── Playbook ──────────────────────────────────────────────────────────

    private static PlaybookDto Playbook(Dictionary<string, string> signals) =>
        RegimeInsight.ComputePlaybook(RegimeInsight.ComputeMarketHealth(signals), signals);

    [Theory]
    [InlineData(100, "Favored")]
    [InlineData(65, "Favored")]
    [InlineData(64, "Neutral")]
    [InlineData(45, "Neutral")]
    [InlineData(44, "Headwinds")]
    [InlineData(0, "Headwinds")]
    public void VerdictOf_BandEdges(int score, string verdict)
    {
        Assert.Equal(verdict, RegimeInsight.VerdictOf(score));
    }

    [Fact]
    public void Playbook_RiskOnRegime_FavorsStocksOverCash()
    {
        var playbook = Playbook(new Dictionary<string, string>
        {
            ["FedFundsRate"] = "Accommodative",
            ["FedBalanceSheet"] = "QE (Expanding)",
            ["M2MoneySupply"] = "Expanding",
            ["ReverseRepo"] = "Low",
            ["RealYield10Y"] = "Negative (Easy)",
            ["TreasuryYield10Y"] = "Normal",
            ["TreasuryYield2Y"] = "Normal",
            ["TreasuryYield3M"] = "Normal",
            ["YieldCurveSpread"] = "Healthy",
            ["YieldSpread10Y3M"] = "Healthy",
            ["CPI"] = "Low",
            ["CorePCE"] = "On Target",
            ["GDP"] = "Expanding",
            ["UnemploymentRate"] = "Low",
            ["JoblessClaims"] = "Low",
            ["SahmRule"] = "No Signal",
            ["RetailSales"] = "Strong",
            ["HousingStarts"] = "Strong",
            ["ConsumerSentiment"] = "Optimistic",
            ["VIX"] = "Calm",
            ["HighYieldSpread"] = "Low",
            ["DollarIndex"] = "Weak USD",
            ["CryptoFearGreed"] = "Neutral",
        });

        Assert.Equal(5, playbook.Assets.Count);
        Assert.Equal("Stocks", playbook.Assets[0].Name);
        Assert.Equal("Favored", playbook.Assets[0].Verdict);
        Assert.Equal("Cash & T-Bills", playbook.Assets[^1].Name);
        Assert.Equal("Headwinds", playbook.Assets[^1].Verdict);
        Assert.Contains("Stocks", playbook.Headline);
    }

    [Fact]
    public void Playbook_RecessionRegime_FavorsCashAndBonds_PunishesRiskAssets()
    {
        var playbook = Playbook(new Dictionary<string, string>
        {
            ["FedFundsRate"] = "Restrictive",
            ["FedBalanceSheet"] = "QT (Contracting)",
            ["M2MoneySupply"] = "Contracting",
            ["ReverseRepo"] = "High",
            ["RealYield10Y"] = "High",
            ["TreasuryYield10Y"] = "High",
            ["TreasuryYield2Y"] = "High",
            ["TreasuryYield3M"] = "High",
            ["YieldCurveSpread"] = "Inverted",
            ["YieldSpread10Y3M"] = "Inverted",
            ["CPI"] = "Low",          // inflation has cooled — the classic recession trade
            ["CorePCE"] = "On Target",
            ["GDP"] = "Contracting",
            ["UnemploymentRate"] = "Elevated",
            ["JoblessClaims"] = "Elevated",
            ["SahmRule"] = "Recession Signal",
            ["RetailSales"] = "Falling",
            ["HousingStarts"] = "Falling",
            ["ConsumerSentiment"] = "Pessimistic",
            ["VIX"] = "Panic",
            ["HighYieldSpread"] = "Stressed",
            ["DollarIndex"] = "Strong USD",
            ["CryptoFearGreed"] = "Extreme Fear",
        });

        Assert.Equal("Cash & T-Bills", playbook.Assets[0].Name);
        Assert.Equal("Long-Term Bonds", playbook.Assets[1].Name);
        Assert.Equal("Favored", playbook.Assets[1].Verdict);

        var stocks = playbook.Assets.Single(a => a.Name == "Stocks");
        var crypto = playbook.Assets.Single(a => a.Name == "Crypto (majors)");
        Assert.Equal("Headwinds", stocks.Verdict);
        Assert.Equal("Headwinds", crypto.Verdict);

        var bonds = playbook.Assets.Single(a => a.Name == "Long-Term Bonds");
        Assert.Contains(bonds.Reasons, r => r.Contains("rate cuts"));
        var cash = playbook.Assets[0];
        Assert.Contains(cash.Reasons, r => r.Contains("paid to wait"));
        Assert.Contains(stocks.Reasons, r => r.Contains("Sahm Rule"));
    }

    [Fact]
    public void Playbook_StagflationRegime_RanksGoldAboveStocksAndBonds()
    {
        var playbook = Playbook(new Dictionary<string, string>
        {
            ["FedFundsRate"] = "Restrictive",
            ["FedBalanceSheet"] = "QT (Contracting)",
            ["M2MoneySupply"] = "Contracting",
            ["ReverseRepo"] = "High",
            ["RealYield10Y"] = "High",
            ["TreasuryYield10Y"] = "High",
            ["TreasuryYield2Y"] = "High",
            ["TreasuryYield3M"] = "High",
            ["YieldCurveSpread"] = "Inverted",
            ["YieldSpread10Y3M"] = "Inverted",
            ["CPI"] = "Elevated",     // inflation still hot — bonds get no rescue
            ["CorePCE"] = "Elevated",
            ["GDP"] = "Slow",
            ["UnemploymentRate"] = "Elevated",
            ["JoblessClaims"] = "Elevated",
            ["SahmRule"] = "Warning",
            ["RetailSales"] = "Flat",
            ["HousingStarts"] = "Falling",
            ["ConsumerSentiment"] = "Pessimistic",
            ["VIX"] = "Elevated",
            ["HighYieldSpread"] = "Elevated",
            ["DollarIndex"] = "Strong USD",
            ["CryptoFearGreed"] = "Fear",
        });

        var names = playbook.Assets.Select(a => a.Name).ToList();
        Assert.True(names.IndexOf("Gold") < names.IndexOf("Stocks"));
        Assert.True(names.IndexOf("Gold") < names.IndexOf("Long-Term Bonds"));

        var gold = playbook.Assets.Single(a => a.Name == "Gold");
        Assert.Contains(gold.Reasons, r => r.Contains("hedge"));
    }

    [Fact]
    public void Playbook_NoSignals_EverythingNeutralWithNoReasons()
    {
        var playbook = Playbook(new Dictionary<string, string>());

        Assert.Equal(5, playbook.Assets.Count);
        Assert.All(playbook.Assets, a =>
        {
            Assert.Equal("Neutral", a.Verdict);
            Assert.Empty(a.Reasons); // mid-range drivers aren't worth a sentence
        });
        Assert.NotEmpty(playbook.Note);
    }

    [Fact]
    public void Playbook_AssetsAlwaysRankedBestFirst()
    {
        var playbook = Playbook(AllWith("Panic"));

        var scores = playbook.Assets.Select(a => a.Score).ToList();
        Assert.Equal(scores.OrderByDescending(s => s), scores);
        Assert.All(playbook.Assets, a => Assert.InRange(a.Score, 0, 100));
    }

    // ── Narrative ─────────────────────────────────────────────────────────

    [Fact]
    public void Summarize_AlwaysEndsWithTheMoverCount()
    {
        var indicators = new Dictionary<string, MacroIndicatorValueDto>
        {
            ["FedFundsRate"] = new(3.63m, "Neutral", "Stable", "neutral", 0, false),
            ["VIX"] = new(35m, "Panic", "Rising", "bad", 3, true),
        };

        var sentences = RegimeInsight.Summarize(indicators);

        Assert.StartsWith("The Fed is roughly neutral (rates at 3.63%)", sentences[0]);
        Assert.Contains("equity volatility is in panic territory", string.Join(" ", sentences));
        Assert.StartsWith("Overall: 1 of 2 indicators are currently market-moving", sentences[^1]);
    }
}
