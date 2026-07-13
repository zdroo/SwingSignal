using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RegimeDeck.Domain.Entities;
using RegimeDeck.Infrastructure.Persistence;

namespace RegimeDeck.Tests.Integration;

// The sector board is a free, public snapshot ranked by regime edge. The
// compute service doesn't run in Testing, so rows are seeded directly.
public class SectorsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public SectorsTests(ApiFactory factory)
    {
        factory.EnsureSchema();
        _factory = factory;
        _client = factory.CreateClient();
        SeedBoard();
    }

    private void SeedBoard()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegimeDeckDbContext>();
        if (db.SectorRotationRows.Any()) return;

        db.SectorRotationRows.AddRange(
            Row("XLK", "Technology", 6.0, 3.2, "Long bias"),
            Row("XLU", "Utilities", -4.0, -1.5, "Stand aside"),
            Row("XLE", "Energy", 11.0, 5.8, "Long bias"));
        db.SaveChanges();
    }

    private static SectorRotationRow Row(string symbol, string sector, double edge, double rs, string stance) =>
        new()
        {
            Symbol = symbol,
            Sector = sector,
            Odds3M = 60,
            Edge3M = edge,
            Stance = stance,
            RelStrength3M = rs,
            ComputedAt = DateTime.UtcNow,
        };

    [Fact]
    public async Task Sectors_Anonymous_RankedByEdge_WithBenchmark()
    {
        var response = await _client.GetAsync("/api/sectors");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal("SPY", root.GetProperty("benchmark").GetString());

        var sectors = root.GetProperty("sectors").EnumerateArray()
            .Select(s => s.GetProperty("symbol").GetString()).ToList();
        Assert.Equal(["XLE", "XLK", "XLU"], sectors); // edge 11 > 6 > -4
    }
}
