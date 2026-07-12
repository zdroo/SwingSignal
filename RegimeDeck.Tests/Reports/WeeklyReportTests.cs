using RegimeDeck.Application.Reports;
using RegimeDeck.Contracts.Regime;
using RegimeDeck.Contracts.Watchlist;

namespace RegimeDeck.Tests.Reports;

public class WeeklyReportScheduleTests
{
    private static readonly DateTime MondayNoon = new(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void DueOnMonday_WhenNeverSent()
    {
        Assert.True(WeeklyReportSchedule.IsDue(MondayNoon, lastSentAt: null));
    }

    [Fact]
    public void NotDue_OnOtherDays()
    {
        Assert.False(WeeklyReportSchedule.IsDue(MondayNoon.AddDays(1), lastSentAt: null));
    }

    [Fact]
    public void NotDue_WhenAlreadySentToday()
    {
        // A restart later the same Monday must not double-send
        Assert.False(WeeklyReportSchedule.IsDue(MondayNoon, lastSentAt: MondayNoon.AddHours(-3)));
    }

    [Fact]
    public void Due_WhenLastSentPreviousMonday()
    {
        Assert.True(WeeklyReportSchedule.IsDue(MondayNoon, lastSentAt: MondayNoon.AddDays(-7)));
    }
}

public class WeeklyReportBuilderTests
{
    private static MacroRegimeDto Regime() => new(
        Indicators: new Dictionary<string, MacroIndicatorValueDto>
        {
            ["VIX"] = new(18m, "Calm", "Stable", "good", 0, false),
        },
        Health: new MarketHealthDto(57, "Steady",
            [new HealthGroupDto("Market Stress", "q", 57, "Steady",
                [new HealthMemberDto("VIX", "Calm", "good")])]),
        Summary: ["Markets are calm & steady."],
        Playbook: new PlaybookDto(
            "Current conditions favor Stocks for new money.",
            "note",
            [new PlaybookAssetDto("Stocks", 72, "Favored", ["reason"])]),
        AsOf: new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Subject_CarriesTheHealthScore()
    {
        Assert.Equal("RegimeDeck Weekly — Market Health 57 (Steady)",
            WeeklyReportBuilder.Subject(Regime()));
    }

    [Fact]
    public void Html_ContainsHealthNarrativePlaybookAndWatchlist()
    {
        var watchlist = new List<WatchlistRowDto>
        {
            new("BTCUSDT", "Bitcoin", 63910m, 46.1, 53.5, -7.4,
                new TradeReadDto("Stand aside", 30, "Moderate", ["r"], "n"), DateTime.UtcNow),
            new("NEWUSDT", "New Coin", null, null, null, null, null, DateTime.UtcNow),
        };

        var html = WeeklyReportBuilder.BuildHtml(Regime(), watchlist, "https://regimedeck.app");

        Assert.Contains("57", html);
        Assert.Contains("Steady", html);
        Assert.Contains("Markets are calm &amp; steady.", html); // narrative is HTML-encoded
        Assert.Contains("Stocks", html);
        Assert.Contains("Favored", html);
        Assert.Contains("BTCUSDT", html);
        Assert.Contains("-7.4pp", html);
        Assert.Contains("Stand aside", html);
        Assert.Contains("pending data", html); // dataless asset degrades, not breaks
        Assert.Contains("https://regimedeck.app/account", html); // manage/opt-out link
        Assert.Contains("not predictions or financial advice", html);
    }

    [Fact]
    public void Html_OmitsTheWatchlistSectionWhenEmpty()
    {
        var html = WeeklyReportBuilder.BuildHtml(Regime(), [], "https://regimedeck.app");
        Assert.DoesNotContain("Your watchlist", html);
    }
}
