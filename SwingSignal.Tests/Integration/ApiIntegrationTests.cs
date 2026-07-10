using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SwingSignal.Tests.Integration;

// End-to-end through the real HTTP pipeline: middleware exception mappings,
// auth gates, the register/login lifecycle, and the Testing-environment
// rate-limiter bypass. What unit tests structurally cannot see.
public class ApiIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(ApiFactory factory)
    {
        factory.EnsureSchema();
        _client = factory.CreateClient();
    }

    private static string UniqueEmail() => $"it-{Guid.NewGuid():N}@test.local";

    private async Task<(string Email, string AccessToken)> RegisterAsync()
    {
        var email = UniqueEmail();
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { email, password = "integration-pass-1" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (email, doc.RootElement.GetProperty("accessToken").GetString()!);
    }

    private static async Task<string> MessageOf(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("message").GetString()!;
    }

    // ── Pipeline basics ──────────────────────────────────────────────────

    [Fact]
    public async Task Health_Returns200()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Exception middleware: domain exceptions → status codes ──────────

    [Fact]
    public async Task Register_ShortPassword_400WithMessageBody()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { email = UniqueEmail(), password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Password must be at least 8 characters.", await MessageOf(response));
    }

    [Fact]
    public async Task Register_DuplicateEmail_409()
    {
        var (email, _) = await RegisterAsync();

        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { email, password = "integration-pass-1" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("An account with this email already exists.", await MessageOf(response));
    }

    [Fact]
    public async Task Login_WrongPassword_401()
    {
        var (email, _) = await RegisterAsync();

        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "wrong-password-1" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Invalid email or password.", await MessageOf(response));
    }

    [Fact]
    public async Task ConfirmEmail_GarbageToken_400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/confirm-email",
            new { token = "not-a-real-token" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("This confirmation link is invalid or has expired.", await MessageOf(response));
    }

    [Fact]
    public async Task Waitlist_InvalidEmail_400_ValidEmail_200()
    {
        var bad = await _client.PostAsJsonAsync("/api/waitlist", new { email = "not-an-email" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var good = await _client.PostAsJsonAsync("/api/waitlist",
            new { email = UniqueEmail(), source = "integration" });
        Assert.Equal(HttpStatusCode.OK, good.StatusCode);
    }

    [Fact]
    public async Task Odds_UnsupportedSymbol_503()
    {
        var (_, token) = await RegisterAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/regime/odds/FAILUSDT");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    // ── Access gates ─────────────────────────────────────────────────────

    [Fact]
    public async Task Odds_NonFlagshipSymbol_Anonymous_401()
    {
        var response = await _client.GetAsync("/api/regime/odds/AAPL");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Odds_FlagshipSymbol_Anonymous_PassesTheGate()
    {
        // BTCUSDT is a flagship (teaser tier): no account needed. With no
        // candle/macro data seeded it returns the empty-odds payload — 200.
        var response = await _client.GetAsync("/api/regime/odds/BTCUSDT");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CustomWindow_Anonymous_401_BadDays_400()
    {
        var gated = await _client.GetAsync("/api/regime/odds/BTCUSDT/period?days=30");
        Assert.Equal(HttpStatusCode.Unauthorized, gated.StatusCode);

        var badDays = await _client.GetAsync("/api/regime/odds/BTCUSDT/period?days=5");
        Assert.Equal(HttpStatusCode.BadRequest, badDays.StatusCode);
    }

    [Fact]
    public async Task Backtest_Anonymous_401()
    {
        var response = await _client.GetAsync("/api/backtest/BTCUSDT?days=90");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UsersMe_WithoutToken_401()
    {
        var response = await _client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Account lifecycle through the real pipeline ──────────────────────

    [Fact]
    public async Task Register_Then_Me_ReturnsProfile()
    {
        var (email, token) = await RegisterAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(email, doc.RootElement.GetProperty("email").GetString());
        Assert.Equal("Free", doc.RootElement.GetProperty("plan").GetString());
    }

    [Fact]
    public async Task DeleteAccount_ThenLogin_401()
    {
        var (email, token) = await RegisterAsync();

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/users/me");
        deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var deleted = await _client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);

        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "integration-pass-1" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_RoundTrip()
    {
        var email = UniqueEmail();
        var register = await _client.PostAsJsonAsync("/api/auth/register",
            new { email, password = "integration-pass-1" });
        using var registerDoc = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
        var refreshToken = registerDoc.RootElement.GetProperty("refreshToken").GetString();

        var refresh = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        using var refreshDoc = JsonDocument.Parse(await refresh.Content.ReadAsStringAsync());
        Assert.Equal(email, refreshDoc.RootElement.GetProperty("email").GetString());
        Assert.False(string.IsNullOrEmpty(refreshDoc.RootElement.GetProperty("accessToken").GetString()));
    }

    // ── Rate limiter: the Testing environment must never throttle ────────

    [Fact]
    public async Task AuthRateLimiter_TestingBypass_NoThrottling()
    {
        // The auth policy allows 10/min in production; 15 rapid attempts must
        // all reach the handler (401), never the limiter (429).
        var email = UniqueEmail();
        for (var i = 0; i < 15; i++)
        {
            var response = await _client.PostAsJsonAsync("/api/auth/login",
                new { email, password = "wrong-password-1" });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
