using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RegimeDeck.Domain.Entities;
using RegimeDeck.Infrastructure.Persistence;

namespace RegimeDeck.Tests.Integration;

// The economic calendar is a free, public snapshot of upcoming high-impact
// macro releases, soonest first. Ingestion doesn't run in Testing, so rows
// are seeded directly.
public class EventsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public EventsTests(ApiFactory factory)
    {
        factory.EnsureSchema();
        _factory = factory;
        _client = factory.CreateClient();
        SeedEvents();
    }

    private void SeedEvents()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegimeDeckDbContext>();
        if (db.EconomicEvents.Any()) return;

        var today = DateTime.UtcNow.Date;
        db.EconomicEvents.AddRange(
            Evt(101, "FOMC Rate Decision", today.AddDays(-2)),  // past — must be filtered out
            Evt(10, "CPI — Inflation", today.AddDays(3)),
            Evt(50, "Jobs Report", today.AddDays(1)),
            Evt(53, "GDP", today.AddDays(10)));
        db.SaveChanges();
    }

    private static EconomicEvent Evt(int releaseId, string title, DateTime date) =>
        new() { ReleaseId = releaseId, Title = title, Date = date, Impact = "High" };

    [Fact]
    public async Task Upcoming_Anonymous_SoonestFirst_PastExcluded()
    {
        var response = await _client.GetAsync("/api/events/upcoming");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var titles = doc.RootElement.GetProperty("events").EnumerateArray()
            .Select(e => e.GetProperty("title").GetString()).ToList();

        Assert.Equal(["Jobs Report", "CPI — Inflation", "GDP"], titles); // +1, +3, +10 days
        Assert.DoesNotContain("FOMC Rate Decision", titles);             // past excluded
    }

    [Fact]
    public async Task Upcoming_RespectsTake()
    {
        var response = await _client.GetAsync("/api/events/upcoming?take=1");

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var events = doc.RootElement.GetProperty("events").EnumerateArray().ToList();

        Assert.Single(events);
        Assert.Equal("Jobs Report", events[0].GetProperty("title").GetString());
    }
}
