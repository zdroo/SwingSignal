using SwingSignal.Domain.Enums;

namespace SwingSignal.Domain.Entities;

public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserPlan Plan { get; set; } = UserPlan.Free;
    public DateTime CreatedAt { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }

    // Email confirmation (unconfirmed accounts are purged after a grace period)
    public bool IsEmailConfirmed { get; set; }
    public string? EmailConfirmationToken { get; set; }
    public DateTime? EmailConfirmationTokenExpiry { get; set; }
    public DateTime? LastConfirmationEmailAt { get; set; }
    public int ConfirmationEmailCount { get; set; }

    // Weekly regime report (Pro): idempotency + opt-out
    public bool WeeklyReportEnabled { get; set; } = true;

    // Change alerts (Pro): watchlist stance flips + market health band moves
    public bool AlertsEnabled { get; set; } = true;

    // Stripe billing linkage; the webhook is the source of truth for Plan.
    // CustomerId survives cancellation so resubscribing reuses the profile.
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public DateTime? LastWeeklyReportAt { get; set; }

    // Password reset
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiry { get; set; }
    public DateTime? LastPasswordResetEmailAt { get; set; }
    public int PasswordResetEmailCount { get; set; }
}
