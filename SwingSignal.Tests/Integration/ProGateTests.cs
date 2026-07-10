using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SwingSignal.Application.Common;
using SwingSignal.Domain.Enums;
using SwingSignal.Infrastructure.Persistence;

namespace SwingSignal.Tests.Integration;

/// Boots the API with the Pro dark-launch flag ON — what production will
/// look like the day Pro ships.
public sealed class ProEnabledApiFactory : ApiFactory
{
    protected override bool ProEnabled => true;
}

// The dark-launch contract: with the flag on, Free hits the gates and Pro
// passes them; with the flag off (see ApiIntegrationTests) nothing changed.
public class ProGateTests : IClassFixture<ProEnabledApiFactory>
{
    private readonly ProEnabledApiFactory _factory;
    private readonly HttpClient _client;

    public ProGateTests(ProEnabledApiFactory factory)
    {
        factory.EnsureSchema();
        _factory = factory;
        _client = factory.CreateClient();
    }

    private const string Password = "integration-pass-1";

    private async Task<string> RegisterAsync(UserPlan plan = UserPlan.Free)
    {
        var email = $"pro-{Guid.NewGuid():N}@test.local";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        if (plan == UserPlan.Free)
            return await TokenOf(response);

        // Upgrade in the database, then log in again — the plan claim is
        // baked into the token, exactly as a real upgrade would behave
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SwingSignalDbContext>();
            var user = db.Users.Single(u => u.Email == email);
            user.Plan = plan;
            db.SaveChanges();
        }

        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return await TokenOf(login);
    }

    private static async Task<string> TokenOf(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static HttpRequestMessage Get(string url, string token) => new(HttpMethod.Get, url)
    {
        Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
    };

    // ── Custom backtest parameters ────────────────────────────────────────

    [Fact]
    public async Task Backtest_ResearchParams_Free_403()
    {
        var token = await RegisterAsync();

        var response = await _client.SendAsync(Get("/api/backtest/SPY?fromYear=2022", token));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Custom backtest parameters are a Pro feature.",
            doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Backtest_StandardRun_Free_NotGated()
    {
        var token = await RegisterAsync();

        var response = await _client.SendAsync(Get("/api/backtest/SPY?days=90&topK=10", token));

        // The honesty proof stays free — whatever else happens, never a paywall
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Backtest_ResearchParams_Pro_NotGated()
    {
        var token = await RegisterAsync(UserPlan.Pro);

        var response = await _client.SendAsync(Get("/api/backtest/SPY?fromYear=2022", token));

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Custom-window daily quota ─────────────────────────────────────────

    [Fact]
    public async Task CustomWindow_Free_CappedAtDailyLimit()
    {
        var token = await RegisterAsync();

        for (var i = 0; i < ProFeatures.CustomWindowDailyLimit; i++)
        {
            var ok = await _client.SendAsync(Get("/api/regime/odds/SPY/period?days=45", token));
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        var over = await _client.SendAsync(Get("/api/regime/odds/SPY/period?days=45", token));

        Assert.Equal(HttpStatusCode.TooManyRequests, over.StatusCode);
        using var doc = JsonDocument.Parse(await over.Content.ReadAsStringAsync());
        Assert.Contains("Pro removes this cap", doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task CustomWindow_Pro_Unlimited()
    {
        var token = await RegisterAsync(UserPlan.Pro);

        for (var i = 0; i < ProFeatures.CustomWindowDailyLimit + 1; i++)
        {
            var response = await _client.SendAsync(Get("/api/regime/odds/SPY/period?days=45", token));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
