using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stripe;
using Stripe.Checkout;
using SwingSignal.Application.Abstractions.Billing;
using SwingSignal.Application.Abstractions.Persistence;
using SwingSignal.Application.Billing;
using SwingSignal.Application.Common;

namespace SwingSignal.Infrastructure.Billing;

public class StripeBillingService : IBillingService
{
    private readonly IUserRepository _users;
    private readonly ILogger<StripeBillingService> _logger;
    private readonly string _frontendUrl;
    private readonly string? _secretKey;
    private readonly string? _webhookSecret;
    private readonly string? _monthlyPriceId;
    private readonly string? _yearlyPriceId;

    public StripeBillingService(
        IUserRepository users, IConfiguration configuration, ILogger<StripeBillingService> logger)
    {
        _users = users;
        _logger = logger;
        _frontendUrl = configuration["Frontend:Url"] ?? "http://localhost:3000";
        _secretKey = configuration["Stripe:SecretKey"];
        _webhookSecret = configuration["Stripe:WebhookSecret"];
        _monthlyPriceId = configuration["Stripe:MonthlyPriceId"];
        _yearlyPriceId = configuration["Stripe:YearlyPriceId"];
    }

    public async Task<string> CreateCheckoutUrlAsync(Guid userId, string period, CancellationToken ct = default)
    {
        if (!BillingRules.IsValidPeriod(period))
            throw new ValidationException("period must be 'monthly' or 'yearly'");

        var client = RequireClient();
        var priceId = period == "monthly" ? _monthlyPriceId : _yearlyPriceId;
        if (string.IsNullOrWhiteSpace(priceId))
            throw new ValidationException("Billing is not configured.");

        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("Account not found.");

        if (user.Plan == Domain.Enums.UserPlan.Pro)
            throw new ConflictException("You already have Pro.");

        var options = new SessionCreateOptions
        {
            Mode = "subscription",
            LineItems = [new SessionLineItemOptions { Price = priceId, Quantity = 1 }],
            // The webhook maps the completed session back to this account
            ClientReferenceId = user.Id.ToString(),
            SuccessUrl = $"{_frontendUrl}/account?upgraded=1",
            CancelUrl = $"{_frontendUrl}/account",
        };

        // Returning customers reuse their Stripe profile; new ones get the
        // email prefilled
        if (!string.IsNullOrWhiteSpace(user.StripeCustomerId))
            options.Customer = user.StripeCustomerId;
        else
            options.CustomerEmail = user.Email;

        var session = await new SessionService(client).CreateAsync(options, cancellationToken: ct);
        return session.Url;
    }

    public async Task<string> CreatePortalUrlAsync(Guid userId, CancellationToken ct = default)
    {
        var client = RequireClient();

        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("Account not found.");

        if (string.IsNullOrWhiteSpace(user.StripeCustomerId))
            throw new ValidationException("No billing profile yet — subscribe first.");

        var session = await new Stripe.BillingPortal.SessionService(client).CreateAsync(
            new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer = user.StripeCustomerId,
                ReturnUrl = $"{_frontendUrl}/account",
            },
            cancellationToken: ct);

        return session.Url;
    }

    public async Task HandleWebhookAsync(string payload, string signature, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_webhookSecret))
            throw new ValidationException("Billing is not configured.");

        Event stripeEvent;
        try
        {
            // Don't hard-fail on an API-version mismatch: we read only a few
            // stable fields, so a dashboard version bump shouldn't drop events
            stripeEvent = EventUtility.ConstructEvent(
                payload, signature, _webhookSecret, throwOnApiVersionMismatch: false);
        }
        catch (StripeException)
        {
            throw new ValidationException("Invalid webhook signature.");
        }

        switch (stripeEvent.Type)
        {
            case "checkout.session.completed"
                when stripeEvent.Data.Object is Stripe.Checkout.Session session:
                await ApplyCheckoutCompletedAsync(session, ct);
                break;

            case "customer.subscription.updated" or "customer.subscription.deleted"
                when stripeEvent.Data.Object is Subscription subscription:
                await ApplySubscriptionStatusAsync(subscription, ct);
                break;

            default:
                // Unsubscribed event types are fine — acknowledge and move on
                break;
        }
    }

    private async Task ApplyCheckoutCompletedAsync(Stripe.Checkout.Session session, CancellationToken ct)
    {
        if (!Guid.TryParse(session.ClientReferenceId, out var userId))
        {
            _logger.LogWarning("Checkout session {Id} has no usable client reference", session.Id);
            return;
        }

        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null)
        {
            _logger.LogWarning("Checkout session {Id} references unknown user {UserId}", session.Id, userId);
            return;
        }

        user.Plan = Domain.Enums.UserPlan.Pro;
        user.StripeCustomerId = session.CustomerId;
        user.StripeSubscriptionId = session.SubscriptionId;
        await _users.UpdateAsync(user, ct);

        _logger.LogInformation("User {UserId} upgraded to Pro via checkout", userId);
    }

    private async Task ApplySubscriptionStatusAsync(Subscription subscription, CancellationToken ct)
    {
        var user = await _users.GetByStripeSubscriptionIdAsync(subscription.Id, ct);
        if (user is null) return; // not one of ours (or checkout event not processed yet)

        var plan = BillingRules.PlanForSubscriptionStatus(subscription.Status);
        if (plan is null || user.Plan == plan) return;

        user.Plan = plan.Value;
        if (plan == Domain.Enums.UserPlan.Free)
            user.StripeSubscriptionId = null; // customer id stays for resubscribing

        await _users.UpdateAsync(user, ct);
        _logger.LogInformation(
            "User {UserId} plan set to {Plan} (subscription {Status})", user.Id, plan, subscription.Status);
    }

    private StripeClient RequireClient() =>
        string.IsNullOrWhiteSpace(_secretKey)
            ? throw new ValidationException("Billing is not configured.")
            : new StripeClient(_secretKey);
}
