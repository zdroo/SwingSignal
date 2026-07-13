using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RegimeDeck.Domain.Entities;
using RegimeDeck.Domain.Enums;
using RegimeDeck.Infrastructure.Persistence;

namespace RegimeDeck.Tests.Integration;

// The screener's two-tier contract through the real HTTP pipeline: the free
// board is public but trimmed to the teaser subset; the full board is Pro-only.
// The compute service doesn't run in Testing, so rows are seeded directly.
public class ScreenerTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ScreenerTests(ApiFactory factory)
    {
        factory.EnsureSchema();
        _factory = factory;
        _client = factory.CreateClient();
        SeedBoard();
    }

    private const string Password = "integration-pass-1";

    // A mix of free-subset symbols and a Pro-only one, so trimming is visible
    private void SeedBoard()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegimeDeckDbContext>();
        if (db.ScreenerRows.Any()) return;

        db.ScreenerRows.AddRange(
            Row("SPY", MarketType.Index, 8.0, "Long bias"),
            Row("BTCUSDT", MarketType.Crypto, 3.0, "No edge"),
            Row("AAPL", MarketType.Stock, 12.0, "Long bias")); // not in the free subset
        db.SaveChanges();
    }

    private static ScreenerRow Row(string symbol, MarketType market, double edge, string stance) =>
        new()
        {
            Symbol = symbol,
            Name = symbol,
            MarketType = market,
            CurrentPrice = 100m,
            Odds3M = 65,
            BaseRate3M = 55,
            Edge3M = edge,
            Stance = stance,
            Strength = "Moderate",
            ComputedAt = DateTime.UtcNow,
        };

    private async Task<string> RegisterAsync(UserPlan plan)
    {
        var email = $"scr-{Guid.NewGuid():N}@test.local";
        var register = await _client.PostAsJsonAsync("/api/auth/register", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        if (plan == UserPlan.Free) return await TokenOf(register);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RegimeDeckDbContext>();
            var user = db.Users.Single(u => u.Email == email);
            user.Plan = plan;
            db.SaveChanges();
        }

        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        return await TokenOf(login);
    }

    private static async Task<string> TokenOf(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static async Task<JsonElement> BodyOf(HttpResponseMessage response)
    {
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    [Fact]
    public async Task FreeBoard_Anonymous_TrimmedToTeaser()
    {
        var response = await _client.GetAsync("/api/screener");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await BodyOf(response);
        Assert.True(body.GetProperty("trimmed").GetBoolean());

        var symbols = body.GetProperty("rows").EnumerateArray()
            .Select(r => r.GetProperty("symbol").GetString()).ToList();
        Assert.Contains("SPY", symbols);
        Assert.Contains("BTCUSDT", symbols);
        Assert.DoesNotContain("AAPL", symbols);           // Pro-only symbol hidden
        Assert.Equal(3, body.GetProperty("universeSize").GetInt32()); // truthful total
    }

    [Fact]
    public async Task FullBoard_Anonymous_401()
    {
        var response = await _client.GetAsync("/api/screener/full");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FullBoard_FreeAccount_403()
    {
        var token = await RegisterAsync(UserPlan.Free);
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/screener/full");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task FullBoard_Pro_SeesWholeBoardRankedByEdge()
    {
        var token = await RegisterAsync(UserPlan.Pro);
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/screener/full");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await BodyOf(response);
        Assert.False(body.GetProperty("trimmed").GetBoolean());

        var symbols = body.GetProperty("rows").EnumerateArray()
            .Select(r => r.GetProperty("symbol").GetString()).ToList();
        Assert.Equal(["AAPL", "SPY", "BTCUSDT"], symbols); // edge 12 > 8 > 3
    }

    [Fact]
    public async Task FullBoard_Pro_StanceFilter()
    {
        var token = await RegisterAsync(UserPlan.Pro);
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/screener/full?stance=Long%20bias");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        var body = await BodyOf(response);
        var symbols = body.GetProperty("rows").EnumerateArray()
            .Select(r => r.GetProperty("symbol").GetString()).ToList();
        Assert.Equal(["AAPL", "SPY"], symbols);
    }
}
