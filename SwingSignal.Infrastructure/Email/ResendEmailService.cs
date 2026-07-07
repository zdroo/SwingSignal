using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Resend;
using SwingSignal.Application.Abstractions.Email;

namespace SwingSignal.Infrastructure.Email;

public class ResendEmailService : IEmailService
{
    private readonly IResend _resend;
    private readonly ILogger<ResendEmailService> _logger;
    private readonly string _from;
    private readonly string _frontendUrl;
    private readonly string? _devRedirectTo;

    public ResendEmailService(IResend resend, IConfiguration configuration, ILogger<ResendEmailService> logger)
    {
        _resend = resend;
        _logger = logger;
        _from = configuration["Resend:From"] ?? "SwingSignal <onboarding@resend.dev>";
        _frontendUrl = configuration["Frontend:Url"] ?? "http://localhost:3000";
        // Resend's sandbox sender only delivers to the account owner —
        // set Resend:DevRedirectTo in dev so all mail lands in your inbox.
        _devRedirectTo = configuration["Resend:DevRedirectTo"];
    }

    public async Task SendWelcomeAsync(string toEmail, CancellationToken ct = default)
    {
        var body = $"""
            <h2 style="margin:0 0 12px;font-size:22px;color:#18181b;">Welcome to SwingSignal 📈</h2>
            <p style="margin:0 0 16px;color:#52525b;">Your account is ready. You can now analyze any symbol, use custom prediction windows, and run backtests.</p>
            <p style="margin:0 0 16px;color:#52525b;">A quick reminder of what we do — and don't do: we show you honest, historically calibrated odds. No buy/sell signals, no promises. You can verify our accuracy yourself on any asset page.</p>
            <a href="{_frontendUrl}/dashboard" style="display:inline-block;background:#059669;color:#fff;text-decoration:none;padding:12px 24px;border-radius:8px;font-weight:600;font-size:14px;">Open the dashboard</a>
            """;
        await SendAsync(toEmail, "Welcome to SwingSignal", body, ct);
    }

    public async Task SendEmailConfirmationAsync(string toEmail, string confirmUrl, CancellationToken ct = default)
    {
        var body = $"""
            <h2 style="margin:0 0 12px;font-size:22px;color:#18181b;">Confirm your email</h2>
            <p style="margin:0 0 16px;color:#52525b;">Click the button below to confirm your SwingSignal account.</p>
            <a href="{confirmUrl}" style="display:inline-block;background:#059669;color:#fff;text-decoration:none;padding:12px 24px;border-radius:8px;font-weight:600;font-size:14px;">Confirm email</a>
            <p style="margin:16px 0 0;color:#6b7280;font-size:13px;">The link expires in 24 hours. If you didn't create an account, you can ignore this email — unconfirmed accounts are deleted automatically.</p>
            """;
        await SendAsync(toEmail, "Confirm your email – SwingSignal", body, ct);
    }

    public async Task SendPasswordResetAsync(string toEmail, string resetUrl, CancellationToken ct = default)
    {
        var body = $"""
            <h2 style="margin:0 0 12px;font-size:22px;color:#18181b;">Reset your password</h2>
            <p style="margin:0 0 16px;color:#52525b;">We received a request to reset the password for your SwingSignal account. Click the button below to choose a new one.</p>
            <a href="{resetUrl}" style="display:inline-block;background:#059669;color:#fff;text-decoration:none;padding:12px 24px;border-radius:8px;font-weight:600;font-size:14px;">Reset password</a>
            <p style="margin:16px 0 0;color:#6b7280;font-size:13px;">The link expires in 1 hour. If you didn't request this, you can safely ignore this email — your password stays unchanged.</p>
            """;
        await SendAsync(toEmail, "Reset your password – SwingSignal", body, ct);
    }

    // ── Helpers ───────────────────────────────────────────────

    private async Task SendAsync(string toEmail, string subject, string bodyHtml, CancellationToken ct)
    {
        try
        {
            var to = string.IsNullOrWhiteSpace(_devRedirectTo) ? toEmail : _devRedirectTo;

            var message = new EmailMessage
            {
                From = _from,
                Subject = subject,
                HtmlBody = WrapLayout(bodyHtml)
            };
            message.To.Add(to);

            await _resend.EmailSendAsync(message, ct);
        }
        catch (Exception ex)
        {
            // Email failures must never break auth flows — log and move on
            _logger.LogError(ex, "Failed to send email '{Subject}' to {Email}", subject, toEmail);
        }
    }

    private static string WrapLayout(string body) => $"""
        <!DOCTYPE html>
        <html lang="en">
        <head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
        <body style="margin:0;padding:0;background:#f4f4f5;font-family:system-ui,-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0">
            <tr><td align="center" style="padding:40px 16px;">
              <table width="560" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:12px;overflow:hidden;border:1px solid #e4e4e7;max-width:560px;">
                <tr><td style="background:#09090b;padding:20px 32px;">
                  <span style="color:#10b981;font-size:20px;font-weight:700;letter-spacing:-0.3px;">SwingSignal</span>
                  <span style="color:#71717a;font-size:12px;margin-left:8px;">Macro Context Dashboard</span>
                </td></tr>
                <tr><td style="padding:32px;">
                  {body}
                </td></tr>
                <tr><td style="border-top:1px solid #f3f4f6;padding:16px 32px;">
                  <p style="margin:0;font-size:12px;color:#9ca3af;">© SwingSignal · Historical data only, not financial advice. If you didn't request this email, you can ignore it.</p>
                </td></tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;
}
