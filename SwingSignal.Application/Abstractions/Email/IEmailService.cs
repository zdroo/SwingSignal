namespace SwingSignal.Application.Abstractions.Email;

/// Transactional email. Implementations must swallow provider failures
/// (log, don't throw) — a mail outage must never break auth flows.
public interface IEmailService
{
    Task SendWelcomeAsync(string toEmail, CancellationToken ct = default);
    Task SendEmailConfirmationAsync(string toEmail, string confirmUrl, CancellationToken ct = default);
    Task SendPasswordResetAsync(string toEmail, string resetUrl, CancellationToken ct = default);
    Task SendWeeklyReportAsync(string toEmail, string subject, string bodyHtml, CancellationToken ct = default);
    Task SendAlertAsync(string toEmail, string subject, string bodyHtml, CancellationToken ct = default);
}
