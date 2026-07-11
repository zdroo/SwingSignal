namespace SwingSignal.Application.Abstractions.Billing;

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
}
