using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RegimeDeck.Tests.Integration;

// End-to-end through the real HTTP pipeline: middleware exception mappings,
// auth gates, the register/login lifecycle, and the Testing-environment
// rate-limiter bypass. What unit tests structurally cannot see.
public class ApiIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;
    private readonly ApiFactory _factory;

    public ApiIntegrationTests(ApiFactory factory)
    {
        factory.EnsureSchema();
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string RefreshCookieValue(HttpResponseMessage res)
    {
        var setCookie = res.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("rd_refresh=", StringComparison.Ordinal));
        return setCookie["rd_refresh=".Length..].Split(';')[0];
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

    // Account-takeover regression. A reset request with no token must be
    // rejected and must NOT change anyone's password. Before the fix, EF Core's
    // `PasswordResetToken == null` → `IS NULL` matched the first user without a
    // pending reset (a freshly-registered account) and the null expiry check
    // passed too, letting an attacker set that user's password.
    [Fact]
    public async Task ResetPassword_NullToken_400_AndVictimPasswordUnchanged()
    {
        var (email, _) = await RegisterAsync(); // fresh user: PasswordResetToken is null

        var attack = await _client.PostAsJsonAsync("/api/auth/reset-password",
            new { token = (string?)null, newPassword = "attacker-chosen-1" });
        Assert.Equal(HttpStatusCode.BadRequest, attack.StatusCode);

        // The victim's original password still works; the attacker's does not
        var hijack = await _client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "attacker-chosen-1" });
        Assert.Equal(HttpStatusCode.Unauthorized, hijack.StatusCode);

        var legit = await _client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "integration-pass-1" });
        Assert.Equal(HttpStatusCode.OK, legit.StatusCode);
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

    // Re-joining the waitlist is common (people forget) — the unique-email index
    // must not surface as a 500; a repeat is an idempotent 200.
    [Fact]
    public async Task Waitlist_DuplicateEmail_IdempotentOk()
    {
        var email = UniqueEmail();
        var first = await _client.PostAsJsonAsync("/api/waitlist", new { email });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await _client.PostAsJsonAsync("/api/waitlist", new { email });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    // A body missing its required field must be a framework 400, never a 500 from
    // dereferencing a null. Nullable is enabled project-wide, so [ApiController]
    // treats the non-nullable Email as required — pin that it actually holds.
    [Fact]
    public async Task Waitlist_MissingEmailField_400_NotNullReference()
    {
        var response = await _client.PostAsJsonAsync("/api/waitlist", new { source = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

    // Assets are created only through validated on-demand ingestion. The old
    // anonymous POST /api/assets (arbitrary attacker-controlled symbol/name into
    // a table the public GetAll serves) was removed — it must not accept writes.
    [Fact]
    public async Task CreateAsset_EndpointRemoved_NotWritable()
    {
        var response = await _client.PostAsJsonAsync("/api/assets",
            new { symbol = "EVILCO", name = "<script>", marketType = "Stock" });

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"POST /api/assets should not be a valid write endpoint, got {(int)response.StatusCode}");
    }

    // A negative limit becomes TOP(-1) -> SqlException (500) on SQL Server; an
    // unbounded one dumps a whole indicator history. Both must be clean 400s.
    // An unknown indicator name is a 400 too. Same for the candles page size.
    [Theory]
    [InlineData("/api/macro/GDP?limit=-1")]
    [InlineData("/api/macro/GDP?limit=0")]
    [InlineData("/api/macro/GDP?limit=999999")]
    [InlineData("/api/macro/NotARealIndicator")]
    [InlineData("/api/candles/BTCUSDT?limit=-1")]
    [InlineData("/api/candles/BTCUSDT?limit=999999")]
    [InlineData("/api/candles/BTCUSDT?interval=NotAnInterval")]
    public async Task ReadEndpoints_BadInput_400(string url)
    {
        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

    // The model binder parses "NaN"/"Infinity" into a double, and an unbounded
    // baseRateYears overflows DateTime.AddYears. Both used to reach the engine and
    // 500 (invalid-JSON NaN / ArgumentOutOfRange). They must be clean 400s now.
    // (Pro flag is off in this factory, so research params aren't gated here.)
    [Theory]
    [InlineData("stateH=NaN")]
    [InlineData("cycleH=Infinity")]
    [InlineData("shrinkM=-1")]
    [InlineData("baseRateYears=100000")]
    [InlineData("fromYear=50000")]
    public async Task Backtest_MalformedResearchParam_400_NotAServerError(string param)
    {
        var (_, token) = await RegisterAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/backtest/SPY?{param}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

    // The refresh token lives only in the HttpOnly cookie the client stores from
    // registration — never in the response body. A bodyless POST to /refresh sends
    // that cookie and gets a fresh access token back. (The test client persists
    // cookies: WebApplicationFactoryClientOptions.HandleCookies defaults to true.)
    [Fact]
    public async Task RefreshToken_RoundTrip_ViaCookie()
    {
        var email = UniqueEmail();
        var register = await _client.PostAsJsonAsync("/api/auth/register",
            new { email, password = "integration-pass-1" });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        using (var registerDoc = JsonDocument.Parse(await register.Content.ReadAsStringAsync()))
            Assert.False(registerDoc.RootElement.TryGetProperty("refreshToken", out _)); // never in the body

        var refresh = await _client.PostAsync("/api/auth/refresh", null);

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        using var refreshDoc = JsonDocument.Parse(await refresh.Content.ReadAsStringAsync());
        Assert.Equal(email, refreshDoc.RootElement.GetProperty("email").GetString());
        Assert.False(string.IsNullOrEmpty(refreshDoc.RootElement.GetProperty("accessToken").GetString()));
    }

    // Reuse detection through the real pipeline: replaying a refresh token that
    // was already rotated away is treated as theft and revokes the whole family,
    // so even the current (legitimate) token dies. Manual cookie control
    // (HandleCookies = false) so we can replay the OLD token after rotation — the
    // default client's cookie jar would have already overwritten it.
    [Fact]
    public async Task RefreshTokenReuse_RevokesTheWholeFamily()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new { email = UniqueEmail(), password = "integration-pass-1" });
        var original = RefreshCookieValue(register);

        // Rotate: refresh with the original token yields a new one
        var rotated = await SendRefresh(client, original);
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var current = RefreshCookieValue(rotated);
        Assert.NotEqual(original, current);

        // Replay the ORIGINAL (now-revoked) token → theft signal → 401
        var replay = await SendRefresh(client, original);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        // The reuse revoked the whole family, so the current token is dead too
        var afterwards = await SendRefresh(client, current);
        Assert.Equal(HttpStatusCode.Unauthorized, afterwards.StatusCode);
    }

    private static Task<HttpResponseMessage> SendRefresh(HttpClient client, string cookieValue)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        req.Headers.Add("Cookie", $"rd_refresh={cookieValue}");
        return client.SendAsync(req);
    }

    [Fact]
    public async Task Logout_RevokesTheSession_RefreshThen401()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/api/auth/register", new { email, password = "integration-pass-1" });

        var logout = await _client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);

        // Cookie cleared and the family revoked — refresh can no longer succeed
        var refresh = await _client.PostAsync("/api/auth/refresh", null);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    // ── Pro dark launch: with the flag OFF, no gate exists at all ─────────

    [Fact]
    public async Task ProFlagOff_ResearchBacktestParams_NotGatedForFreeUsers()
    {
        var (_, token) = await RegisterAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/backtest/SPY?fromYear=2022");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
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
