using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using RegimeDeck.Domain.Enums;
using RegimeDeck.Infrastructure.Persistence;

namespace RegimeDeck.Tests.Integration;

/// Boots the API with a Stripe webhook secret configured, so a signed
/// payload passes real verification through the whole HTTP pipeline.
public sealed class BillingWebhookApiFactory : ApiFactory
{
    public const string Secret = "whsec_integration_test_0123456789";
    protected override string? StripeWebhookSecret => Secret;
}

// End-to-end proof that the webhook route reads the raw body + signature
// header and drives a real plan change — the piece the service-level
// StripeWebhookTests don't exercise.
public class BillingWebhookHttpTests : IClassFixture<BillingWebhookApiFactory>
{
    private readonly BillingWebhookApiFactory _factory;
    private readonly HttpClient _client;

    public BillingWebhookHttpTests(BillingWebhookApiFactory factory)
    {
        factory.EnsureSchema();
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string Sign(string payload)
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(BillingWebhookApiFactory.Secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{ts}.{payload}"));
        return $"t={ts},v1={Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private async Task<Guid> RegisterAsync()
    {
        var email = $"wh-{Guid.NewGuid():N}@test.local";
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { email, password = "integration-pass-1" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegimeDeckDbContext>();
        return db.Users.Single(u => u.Email == email).Id;
    }

    private UserPlan PlanOf(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegimeDeckDbContext>();
        return db.Users.Single(u => u.Id == userId).Plan;
    }

    [Fact]
    public async Task SignedCheckoutCompleted_UpgradesUser_ThroughTheHttpPipeline()
    {
        var userId = await RegisterAsync();
        Assert.Equal(UserPlan.Free, PlanOf(userId));

        var payload = """
            {"id":"evt_http_1","object":"event","type":"checkout.session.completed",
             "data":{"object":{"id":"cs_http_1","object":"checkout.session",
               "client_reference_id":"USER_ID","customer":"cus_http_1","subscription":"sub_http_1"}}}
            """.Replace("USER_ID", userId.ToString());

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/billing/webhook")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Stripe-Signature", Sign(payload));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(UserPlan.Pro, PlanOf(userId));
    }
}
