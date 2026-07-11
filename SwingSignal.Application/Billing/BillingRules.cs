using SwingSignal.Domain.Enums;

namespace SwingSignal.Application.Billing;

/// How Stripe subscription statuses map to plans — the business decision,
/// kept out of the Stripe SDK code so it's unit-testable.
public static class BillingRules
{
    /// Null means "no plan change" (transitional statuses).
    public static UserPlan? PlanForSubscriptionStatus(string status) => status switch
    {
        // past_due keeps Pro: Stripe is retrying the card (dunning) — access
        // is cut only when the subscription is actually canceled/expired
        "active" or "trialing" or "past_due" => UserPlan.Pro,
        "canceled" or "unpaid" or "incomplete_expired" => UserPlan.Free,
        _ => null, // incomplete, paused — wait for a definitive status
    };

    public static bool IsValidPeriod(string period) => period is "monthly" or "yearly";
}
