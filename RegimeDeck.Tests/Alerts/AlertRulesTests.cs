using RegimeDeck.Application.Alerts;
using RegimeDeck.Contracts.Regime;

namespace RegimeDeck.Tests.Alerts;

// The alert contract: fire only on a real change from a known previous
// state; the first-ever evaluation records silently instead of spamming.
public class AlertRulesTests
{
    private static MarketHealthDto Health(string label, int score = 50) =>
        new(score, label, [new HealthGroupDto("g", "q", score, label, [new HealthMemberDto("VIX", "Calm", "good")])]);

    private static TradeReadDto Read(string stance) =>
        new(stance, 90, "Moderate", ["the regime shifted the odds"], "note");

    // ── Market health ─────────────────────────────────────────────────────

    [Fact]
    public void HealthChange_FirstEvaluation_RecordsWithoutNotifying()
    {
        var change = AlertRules.HealthChange(previous: null, Health("Steady"));

        Assert.NotNull(change);
        Assert.False(change.Notify);
        Assert.Equal("Steady", change.NewValue);
    }

    [Fact]
    public void HealthChange_BandCrossing_Notifies()
    {
        var change = AlertRules.HealthChange("Steady", Health("Strained", 30));

        Assert.NotNull(change);
        Assert.True(change.Notify);
        Assert.Equal("Market Health moved: Steady → Strained (30/100)", change.Title);
    }

    [Fact]
    public void HealthChange_SameBand_Null()
    {
        Assert.Null(AlertRules.HealthChange("Steady", Health("Steady")));
    }

    [Fact]
    public void HealthChange_NoData_Null()
    {
        // An empty regime (ingestion outage) must not read as a band change
        Assert.Null(AlertRules.HealthChange("Steady", new MarketHealthDto(0, "Stressed", [])));
    }

    // ── Stance ────────────────────────────────────────────────────────────

    [Fact]
    public void StanceChange_FirstEvaluation_RecordsWithoutNotifying()
    {
        var change = AlertRules.StanceChange("SPY", previous: null, Read("No edge"));

        Assert.NotNull(change);
        Assert.False(change.Notify);
        Assert.Equal("stance:SPY", change.Key);
    }

    [Fact]
    public void StanceChange_Flip_NotifiesWithReason()
    {
        var change = AlertRules.StanceChange("BTCUSDT", "Stand aside", Read("Long bias"));

        Assert.NotNull(change);
        Assert.True(change.Notify);
        Assert.Equal("BTCUSDT statistical read flipped: Stand aside → Long bias", change.Title);
        Assert.Equal("the regime shifted the odds", change.Detail);
    }

    [Fact]
    public void StanceChange_SameStance_Null()
    {
        Assert.Null(AlertRules.StanceChange("SPY", "No edge", Read("No edge")));
    }

    [Fact]
    public void StanceChange_NoComputableOdds_Null()
    {
        // A data gap keeps the old state instead of firing a phantom change
        Assert.Null(AlertRules.StanceChange("SPY", "Long bias", read: null));
    }
}

public class AlertEmailBuilderTests
{
    [Fact]
    public void SingleChange_SubjectCarriesTheTitle()
    {
        var change = AlertRules.StanceChange("BTCUSDT", "Stand aside",
            new TradeReadDto("Long bias", 90, "Strong", ["r"], "n"))!;

        Assert.Equal(
            "RegimeDeck Alert — BTCUSDT statistical read flipped: Stand aside → Long bias",
            AlertEmailBuilder.Subject([change]));
    }

    [Fact]
    public void Html_ListsEveryChangeWithManageLink()
    {
        var changes = new[]
        {
            AlertRules.HealthChange("Steady", new MarketHealthDto(30, "Strained",
                [new HealthGroupDto("g", "q", 30, "Strained", [new HealthMemberDto("VIX", "Panic", "bad")])]))!,
            AlertRules.StanceChange("SPY", "No edge",
                new TradeReadDto("Long bias", 90, "Moderate", ["reason line"], "n"))!,
        };

        var html = AlertEmailBuilder.BuildHtml(changes, "https://regimedeck.app");

        Assert.Contains("Market Health moved: Steady → Strained (30/100)", html);
        Assert.Contains("SPY statistical read flipped", html);
        Assert.Contains("reason line", html);
        Assert.Contains("https://regimedeck.app/account", html);
        Assert.Contains("not predictions or financial advice", html);
        Assert.Equal("RegimeDeck Alert — 2 changes in what you track", AlertEmailBuilder.Subject(changes));
    }
}
