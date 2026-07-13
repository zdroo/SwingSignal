using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RegimeDeck.Application.Watchlist;
using RegimeDeck.Domain.Enums;
using RegimeDeck.Infrastructure.Persistence;

namespace RegimeDeck.Tests.Integration;

// Watchlist is Pro from birth: the plan claim is the entire gate, active
// regardless of the dark-launch flag (which is OFF here, as in production).
public class WatchlistTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public WatchlistTests(ApiFactory factory)
    {
        factory.EnsureSchema();
        _factory = factory;
        _client = factory.CreateClient();
    }

    private const string Password = "integration-pass-1";

    private async Task<string> RegisterAsync(UserPlan plan)
    {
        var email = $"wl-{Guid.NewGuid():N}@test.local";
        var register = await _client.PostAsJsonAsync("/api/auth/register", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        if (plan == UserPlan.Free)
            return await TokenOf(register);

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

    private static HttpRequestMessage Request(HttpMethod method, string url, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
        };
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    // ── The gate ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Anonymous_401()
    {
        var response = await _client.GetAsync("/api/watchlist");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FreeAccount_403_EvenWithFlagOff()
    {
        var token = await RegisterAsync(UserPlan.Free);

        var response = await _client.SendAsync(Request(HttpMethod.Get, "/api/watchlist", token));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── The lifecycle ─────────────────────────────────────────────────────

    [Fact]
    public async Task Pro_AddListRemove_FullRoundTrip()
    {
        var token = await RegisterAsync(UserPlan.Pro);

        var add = await _client.SendAsync(Request(HttpMethod.Post, "/api/watchlist", token, new { symbol = "spy" }));
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        using (var doc = JsonDocument.Parse(await add.Content.ReadAsStringAsync()))
            Assert.Equal("SPY", doc.RootElement.GetProperty("symbol").GetString());

        var list = await _client.SendAsync(Request(HttpMethod.Get, "/api/watchlist", token));
        using (var doc = JsonDocument.Parse(await list.Content.ReadAsStringAsync()))
            Assert.Single(doc.RootElement.EnumerateArray());

        var duplicate = await _client.SendAsync(Request(HttpMethod.Post, "/api/watchlist", token, new { symbol = "SPY" }));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var remove = await _client.SendAsync(Request(HttpMethod.Delete, "/api/watchlist/SPY", token));
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);

        var removeAgain = await _client.SendAsync(Request(HttpMethod.Delete, "/api/watchlist/SPY", token));
        Assert.Equal(HttpStatusCode.NotFound, removeAgain.StatusCode);
    }

    [Fact]
    public async Task UnsupportedSymbol_400()
    {
        var token = await RegisterAsync(UserPlan.Pro);

        var response = await _client.SendAsync(
            Request(HttpMethod.Post, "/api/watchlist", token, new { symbol = "FAILUSDT" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CapAtMaxItems()
    {
        var token = await RegisterAsync(UserPlan.Pro);

        for (var i = 0; i < WatchlistService.MaxItems; i++)
        {
            var add = await _client.SendAsync(
                Request(HttpMethod.Post, "/api/watchlist", token, new { symbol = $"WL{i}USDT" }));
            Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        }

        var over = await _client.SendAsync(
            Request(HttpMethod.Post, "/api/watchlist", token, new { symbol = "ONEMOREUSDT" }));

        Assert.Equal(HttpStatusCode.BadRequest, over.StatusCode);
        using var doc = JsonDocument.Parse(await over.Content.ReadAsStringAsync());
        Assert.Contains("limited to", doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Overview_DegradesToNameOnlyRows_WithoutCandleData()
    {
        var token = await RegisterAsync(UserPlan.Pro);
        await _client.SendAsync(Request(HttpMethod.Post, "/api/watchlist", token, new { symbol = "SPY" }));
        await _client.SendAsync(Request(HttpMethod.Post, "/api/watchlist", token, new { symbol = "BTCUSDT" }));

        var response = await _client.SendAsync(Request(HttpMethod.Get, "/api/watchlist/overview", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = doc.RootElement.EnumerateArray().ToList();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(JsonValueKind.Null, r.GetProperty("odds3M").ValueKind));
    }
}
