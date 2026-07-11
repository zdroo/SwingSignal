using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SwingSignal.Application.Abstractions.Persistence;
using SwingSignal.Application.Common;
using SwingSignal.Domain.Entities;
using SwingSignal.Domain.Enums;
using SwingSignal.Infrastructure.Billing;

namespace SwingSignal.Tests.Billing;

// The webhook is the single source of truth for a paid plan, so its happy
// path deserves a real test: sign payloads exactly as Stripe does and drive
// them through genuine signature verification + SDK deserialization.
public class StripeWebhookTests
{
    private const string Secret = "whsec_test_secret_0123456789abcdef";

    private static StripeBillingService Service(StubUserRepository repo)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Stripe:SecretKey"] = "sk_test_x",
                ["Stripe:WebhookSecret"] = Secret,
                ["Frontend:Url"] = "http://localhost:3000",
            })
            .Build();

        return new StripeBillingService(repo, config, NullLogger<StripeBillingService>.Instance);
    }

    // Stripe's scheme: header "t=<ts>,v1=<hmac_sha256_hex(secret, "ts.payload")>"
    private static string Sign(string payload)
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{ts}.{payload}"));
        return $"t={ts},v1={Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    [Fact]
    public async Task CheckoutCompleted_UpgradesUserToPro_AndLinksStripeIds()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "u@test.local", Plan = UserPlan.Free };
        var repo = new StubUserRepository(user);
        var service = Service(repo);

        var payload = """
            {"id":"evt_1","object":"event","type":"checkout.session.completed",
             "data":{"object":{"id":"cs_1","object":"checkout.session",
               "client_reference_id":"USER_ID","customer":"cus_1","subscription":"sub_1"}}}
            """.Replace("USER_ID", user.Id.ToString());

        await service.HandleWebhookAsync(payload, Sign(payload));

        Assert.Equal(UserPlan.Pro, user.Plan);
        Assert.Equal("cus_1", user.StripeCustomerId);
        Assert.Equal("sub_1", user.StripeSubscriptionId);
    }

    [Fact]
    public async Task SubscriptionDeleted_DowngradesToFree_KeepsCustomerId()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "u@test.local",
            Plan = UserPlan.Pro,
            StripeCustomerId = "cus_1",
            StripeSubscriptionId = "sub_1",
        };
        var repo = new StubUserRepository(user);
        var service = Service(repo);

        var payload = """
            {"id":"evt_2","object":"event","type":"customer.subscription.deleted",
             "data":{"object":{"id":"sub_1","object":"subscription","status":"canceled"}}}
            """;

        await service.HandleWebhookAsync(payload, Sign(payload));

        Assert.Equal(UserPlan.Free, user.Plan);
        Assert.Null(user.StripeSubscriptionId);       // cleared
        Assert.Equal("cus_1", user.StripeCustomerId);  // kept, so resubscribe reuses it
    }

    [Fact]
    public async Task SubscriptionPastDue_KeepsProDuringDunning()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "u@test.local",
            Plan = UserPlan.Pro,
            StripeCustomerId = "cus_1",
            StripeSubscriptionId = "sub_1",
        };
        var service = Service(new StubUserRepository(user));

        var payload = """
            {"id":"evt_3","object":"event","type":"customer.subscription.updated",
             "data":{"object":{"id":"sub_1","object":"subscription","status":"past_due"}}}
            """;

        await service.HandleWebhookAsync(payload, Sign(payload));

        Assert.Equal(UserPlan.Pro, user.Plan);
        Assert.Equal("sub_1", user.StripeSubscriptionId);
    }

    [Fact]
    public async Task ForgedSignature_Rejected_NoPlanChange()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "u@test.local", Plan = UserPlan.Free };
        var service = Service(new StubUserRepository(user));

        var payload = """
            {"id":"evt_4","object":"event","type":"checkout.session.completed",
             "data":{"object":{"id":"cs_9","object":"checkout.session",
               "client_reference_id":"USER_ID","customer":"cus_9","subscription":"sub_9"}}}
            """.Replace("USER_ID", user.Id.ToString());

        await Assert.ThrowsAsync<ValidationException>(
            () => service.HandleWebhookAsync(payload, "t=123,v1=deadbeef"));

        Assert.Equal(UserPlan.Free, user.Plan); // never touched
    }

    [Fact]
    public async Task UnknownSubscription_Ignored()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "u@test.local", Plan = UserPlan.Pro };
        var service = Service(new StubUserRepository(user));

        // A subscription id we don't have on file — must no-op, not throw
        var payload = """
            {"id":"evt_5","object":"event","type":"customer.subscription.deleted",
             "data":{"object":{"id":"sub_unknown","object":"subscription","status":"canceled"}}}
            """;

        await service.HandleWebhookAsync(payload, Sign(payload));

        Assert.Equal(UserPlan.Pro, user.Plan);
    }

    // Minimal repo: only the members StripeBillingService touches are real.
    private sealed class StubUserRepository : IUserRepository
    {
        private readonly List<User> _users;
        public StubUserRepository(params User[] users) => _users = [.. users];

        public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

        public Task<User?> GetByStripeSubscriptionIdAsync(string subscriptionId, CancellationToken ct = default) =>
            Task.FromResult(_users.FirstOrDefault(u => u.StripeSubscriptionId == subscriptionId));

        public Task UpdateAsync(User user, CancellationToken ct = default) => Task.CompletedTask;

        public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<User?> GetByRefreshTokenAsync(string t, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<User?> GetByEmailConfirmationTokenAsync(string t, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<User?> GetByPasswordResetTokenAsync(string t, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> DeleteUnconfirmedOlderThanAsync(DateTime c, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<User>> GetWeeklyReportRecipientsAsync(DateTime s, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<User>> GetAlertRecipientsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(User user, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(User user, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
