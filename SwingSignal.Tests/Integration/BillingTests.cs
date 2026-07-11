using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SwingSignal.Tests.Integration;

// Billing is gated two ways: authenticated user endpoints, and the whole
// feature is invisible (404) until the Pro dark-launch flag is on.
public class BillingTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public BillingTests(ApiFactory factory)
    {
        factory.EnsureSchema();
        _client = factory.CreateClient();
    }

    private async Task<string> RegisterAsync()
    {
        var email = $"bill-{Guid.NewGuid():N}@test.local";
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { email, password = "integration-pass-1" });
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static HttpRequestMessage Post(string url, string token, object? body = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
        };
        if (body is not null) req.Content = JsonContent.Create(body);
        return req;
    }

    [Fact]
    public async Task Checkout_Anonymous_401()
    {
        var response = await _client.PostAsJsonAsync("/api/billing/checkout", new { period = "monthly" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_FlagOff_404_FeatureHidden()
    {
        var token = await RegisterAsync();
        var response = await _client.SendAsync(Post("/api/billing/checkout", token, new { period = "monthly" }));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Portal_FlagOff_404_FeatureHidden()
    {
        var token = await RegisterAsync();
        var response = await _client.SendAsync(Post("/api/billing/portal", token));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_BadSignature_400_NotAServerError()
    {
        // The webhook is reachable regardless of the flag (Stripe calls it),
        // but an unverifiable payload is a clean 400, never a 500
        var response = await _client.PostAsync("/api/billing/webhook",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

// With the flag ON: endpoints exist, but respond honestly that billing is
// unconfigured (no Stripe keys in the test host) rather than crashing.
public class BillingFlagOnTests : IClassFixture<ProEnabledApiFactory>
{
    private readonly HttpClient _client;

    public BillingFlagOnTests(ProEnabledApiFactory factory)
    {
        factory.EnsureSchema();
        _client = factory.CreateClient();
    }

    private async Task<string> RegisterAsync()
    {
        var email = $"billon-{Guid.NewGuid():N}@test.local";
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { email, password = "integration-pass-1" });
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("accessToken").GetString()!;
    }

    [Fact]
    public async Task Checkout_InvalidPeriod_400()
    {
        var token = await RegisterAsync();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/billing/checkout")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            Content = JsonContent.Create(new { period = "weekly" }),
        };
        var response = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Portal_WhenBillingUnconfigured_400_NotAServerError()
    {
        // The test host has no Stripe keys — the endpoint must fail politely
        // (400), never crash (500). The full transition path is covered by
        // StripeWebhookTests with real signed events.
        var token = await RegisterAsync();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/billing/portal")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
        };
        var response = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
