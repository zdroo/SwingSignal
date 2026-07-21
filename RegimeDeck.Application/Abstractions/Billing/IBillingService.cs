namespace RegimeDeck.Application.Abstractions.Billing;

/// Subscription billing. The webhook is the single source of truth for a
/// user's plan — checkout only starts the flow, it never sets Plan itself.
public interface IBillingService
{
    /// Hosted checkout page URL for the given billing period ("monthly" | "yearly").
    Task<string> CreateCheckoutUrlAsync(Guid userId, string period, CancellationToken ct = default);

    /// Hosted customer portal URL (manage payment method, cancel, invoices).
    Task<string> CreatePortalUrlAsync(Guid userId, CancellationToken ct = default);

    /// Verifies and applies a provider webhook event.
    Task HandleWebhookAsync(string payload, string signature, CancellationToken ct = default);

    /// Cancels the user's active subscription at the provider, if any. Best-effort
    /// and idempotent: a no-op when the user has no subscription, and it logs and
    /// swallows provider errors rather than throwing — it is called during account
    /// deletion, which must never be blocked by a billing-provider outage.
    Task CancelSubscriptionAsync(Guid userId, CancellationToken ct = default);
}
