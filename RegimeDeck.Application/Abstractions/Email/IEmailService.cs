namespace RegimeDeck.Application.Abstractions.Email;

/// Transactional email. Implementations must swallow provider failures
/// (log, don't throw) — a mail outage must never break auth flows.
public interface IEmailService
{
    // The confirmation email carries the welcome content too — a separate welcome
    // send was dropped to halve per-signup email volume (free-tier friendly).
    Task SendEmailConfirmationAsync(string toEmail, string confirmUrl, CancellationToken ct = default);
    Task SendPasswordResetAsync(string toEmail, string resetUrl, CancellationToken ct = default);
    Task SendWeeklyReportAsync(string toEmail, string subject, string bodyHtml, CancellationToken ct = default);
    Task SendAlertAsync(string toEmail, string subject, string bodyHtml, CancellationToken ct = default);
}
