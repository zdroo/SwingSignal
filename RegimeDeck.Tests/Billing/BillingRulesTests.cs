using RegimeDeck.Application.Billing;
using RegimeDeck.Domain.Enums;

namespace RegimeDeck.Tests.Billing;

// How Stripe subscription statuses map to a plan is the business rule that
// decides who has access — pinned exactly, including the dunning case.
public class BillingRulesTests
{
    [Theory]
    [InlineData("active", UserPlan.Pro)]
    [InlineData("trialing", UserPlan.Pro)]
    [InlineData("past_due", UserPlan.Pro)]   // Stripe is retrying the card — keep access
    [InlineData("canceled", UserPlan.Free)]
    [InlineData("unpaid", UserPlan.Free)]
    [InlineData("incomplete_expired", UserPlan.Free)]
    public void PlanForSubscriptionStatus_DefinitiveStatuses(string status, UserPlan expected)
    {
        Assert.Equal(expected, BillingRules.PlanForSubscriptionStatus(status));
    }

    [Theory]
    [InlineData("incomplete")]
    [InlineData("paused")]
    [InlineData("something_new")]
    public void PlanForSubscriptionStatus_TransitionalStatuses_NoChange(string status)
    {
        Assert.Null(BillingRules.PlanForSubscriptionStatus(status));
    }

    [Theory]
    [InlineData("monthly", true)]
    [InlineData("yearly", true)]
    [InlineData("weekly", false)]
    [InlineData("", false)]
    [InlineData("MONTHLY", false)]
    public void IsValidPeriod(string period, bool expected)
    {
        Assert.Equal(expected, BillingRules.IsValidPeriod(period));
    }
}
