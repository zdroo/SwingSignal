using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using RegimeDeck.Application.Abstractions.Billing;
using RegimeDeck.Application.Abstractions.Email;
using RegimeDeck.Application.Common;
using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Application.Abstractions.Security;
using RegimeDeck.Contracts.Auth;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Application.Auth;

public class AuthService : IAuthService
{
    // The session length lives here (the access token is short — 1h — and rotated
    // silently). Rotation issues a fresh 365-day token each time, so an active
    // user effectively never has to sign in again.
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(365);
    private static readonly TimeSpan ConfirmationTokenLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromHours(1);

    // Per-user email throttling: minimum gap between sends, max sends per rolling hour
    private static readonly TimeSpan EmailCooldown = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan EmailWindow = TimeSpan.FromHours(1);
    private const int MaxEmailsPerWindow = 5;

    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokens;
    private readonly IGoogleTokenValidator _google;
    private readonly IEmailService _email;
    private readonly IAnalyticsRepository _analytics;
    private readonly IBillingService _billing;
    private readonly string _frontendUrl;

    public AuthService(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher passwordHasher,
        ITokenService tokens,
        IGoogleTokenValidator google,
        IEmailService email,
        IAnalyticsRepository analytics,
        IBillingService billing,
        IConfiguration config)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _passwordHasher = passwordHasher;
        _tokens = tokens;
        _google = google;
        _email = email;
        _analytics = analytics;
        _billing = billing;
        _frontendUrl = config["Frontend:Url"] ?? "http://localhost:3000";
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, string? userAgent, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || email.Length > 256)
            throw new ValidationException("A valid email address is required.");

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            throw new ValidationException("Password must be at least 8 characters.");

        if (await _users.ExistsByEmailAsync(email, ct))
            throw new ConflictException("An account with this email already exists.");

        var user = new User
        {
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            CreatedAt = DateTime.UtcNow
        };

        var confirmToken = IssueConfirmationToken(user);
        await _users.AddAsync(user, ct);

        // Fire-and-forget semantics: a mail outage must not block registration
        await _email.SendEmailConfirmationAsync(user.Email, ConfirmUrl(confirmToken), ct);

        return await OpenSessionAsync(user, userAgent, ct);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, string? userAgent, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _users.GetByEmailAsync(email, ct);
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new AuthenticationFailedException("Invalid email or password.");

        return await OpenSessionAsync(user, userAgent, ct);
    }

    public async Task<AuthResult> RefreshAsync(string? refreshToken, string? userAgent, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new AuthenticationFailedException("Session is invalid. Please sign in again.");

        var existing = await _refreshTokens.GetByHashAsync(HashToken(refreshToken), ct);
        if (existing is null)
            throw new AuthenticationFailedException("Session is invalid. Please sign in again.");

        var now = DateTime.UtcNow;

        // Reuse detection: a token that was already rotated away is being replayed.
        // With client-side single-flight this shouldn't happen on the honest path —
        // it signals theft. Revoke the whole family and force a fresh sign-in.
        if (existing.RevokedAt is not null)
        {
            await _refreshTokens.RevokeFamilyAsync(existing.FamilyId, now, ct);
            throw new AuthenticationFailedException("Session is invalid. Please sign in again.");
        }

        if (existing.ExpiresAt <= now)
            throw new AuthenticationFailedException("Session has expired. Please sign in again.");

        var user = await _users.GetByIdAsync(existing.UserId, ct);
        if (user is null)
            throw new AuthenticationFailedException("Session is invalid. Please sign in again.");

        // Rotate within the same family (same session/device continues)
        var (replacement, raw) = NewRefreshToken(user.Id, existing.FamilyId, userAgent);
        await _refreshTokens.RotateAsync(existing, replacement, ct);

        return BuildResult(user, raw);
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;

        var existing = await _refreshTokens.GetByHashAsync(HashToken(refreshToken), ct);
        if (existing is null) return;

        // Kill the whole rotation chain, so a stolen mid-chain token can't continue
        await _refreshTokens.RevokeFamilyAsync(existing.FamilyId, DateTime.UtcNow, ct);
    }

    public async Task<AuthResult> GoogleLoginAsync(GoogleLoginRequest request, string? userAgent, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
            throw new AuthenticationFailedException("Google ID token required.");

        var googleUser = await _google.ValidateAsync(request.IdToken, ct);
        var email = googleUser.Email.Trim().ToLowerInvariant();

        var user = await _users.GetByEmailAsync(email, ct);

        if (user is null)
        {
            // First Google sign-in: create the account. Google has verified the
            // email, so it's confirmed from the start. The random password hash
            // means password login stays impossible until the user sets one.
            user = new User
            {
                Email = email,
                PasswordHash = _passwordHasher.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),
                CreatedAt = DateTime.UtcNow,
                IsEmailConfirmed = true
            };

            await _users.AddAsync(user, ct);
            await _email.SendWelcomeAsync(user.Email, ct);
        }
        else if (!user.IsEmailConfirmed)
        {
            user.IsEmailConfirmed = true; // Google ownership proof supersedes pending confirmation
            await _users.UpdateAsync(user, ct);
        }

        return await OpenSessionAsync(user, userAgent, ct);
    }

    public async Task ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            throw new ValidationException("Confirmation token required.");

        var user = await _users.GetByEmailConfirmationTokenAsync(request.Token, ct);

        if (user is null || user.EmailConfirmationTokenExpiry < DateTime.UtcNow)
            throw new ValidationException("This confirmation link is invalid or has expired.");

        var firstConfirmation = !user.IsEmailConfirmed;

        user.IsEmailConfirmed = true;
        user.EmailConfirmationToken = null;
        user.EmailConfirmationTokenExpiry = null;
        await _users.UpdateAsync(user, ct);

        if (firstConfirmation)
            await _email.SendWelcomeAsync(user.Email, ct);
    }

    public async Task ResendConfirmationAsync(ResendConfirmationRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _users.GetByEmailAsync(email, ct);

        // Never reveal whether the email exists
        if (user is null || user.IsEmailConfirmed) return;

        // Silently skip when throttled — see IsEmailThrottled for why we must not
        // surface a distinguishable error on this enumeration-safe endpoint.
        if (IsEmailThrottled(user.LastConfirmationEmailAt, user.ConfirmationEmailCount)) return;

        var token = IssueConfirmationToken(user);
        await _users.UpdateAsync(user, ct);
        await _email.SendEmailConfirmationAsync(user.Email, ConfirmUrl(token), ct);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _users.GetByEmailAsync(email, ct);

        // Never reveal whether the email exists
        if (user is null) return;

        // Silently skip when throttled — a distinguishable error here would leak
        // that the address is registered (see IsEmailThrottled).
        if (IsEmailThrottled(user.LastPasswordResetEmailAt, user.PasswordResetEmailCount)) return;

        var now = DateTime.UtcNow;
        if (user.LastPasswordResetEmailAt is null || now - user.LastPasswordResetEmailAt > EmailWindow)
            user.PasswordResetEmailCount = 0;
        user.PasswordResetEmailCount++;
        user.LastPasswordResetEmailAt = now;

        user.PasswordResetToken = NewToken();
        user.PasswordResetTokenExpiry = now.Add(ResetTokenLifetime);

        await _users.UpdateAsync(user, ct);
        await _email.SendPasswordResetAsync(
            user.Email, $"{_frontendUrl}/auth/reset-password?token={user.PasswordResetToken}", ct);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            throw new ValidationException("Reset token required.");

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            throw new ValidationException("Password must be at least 8 characters.");

        var user = await _users.GetByPasswordResetTokenAsync(request.Token, ct);

        if (user is null || user.PasswordResetTokenExpiry < DateTime.UtcNow)
            throw new ValidationException("This reset link is invalid or has expired.");

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiry = null;
        // A password reset proves email ownership just as well as the confirm link
        user.IsEmailConfirmed = true;
        await _users.UpdateAsync(user, ct);

        // Someone resetting a password may be locking out an intruder — drop every
        // existing session. The user signs in fresh afterward.
        await _refreshTokens.RevokeAllForUserAsync(user.Id, DateTime.UtcNow, ct);
    }

    public async Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("Account not found.");

        return new UserProfileDto(
            user.Email, user.Plan.ToString(), user.IsEmailConfirmed, user.CreatedAt,
            user.WeeklyReportEnabled, user.AlertsEnabled,
            HasBilling: user.StripeCustomerId is not null);
    }

    public async Task SetWeeklyReportAsync(Guid userId, bool enabled, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("Account not found.");

        user.WeeklyReportEnabled = enabled;
        await _users.UpdateAsync(user, ct);
    }

    public async Task SetAlertsAsync(Guid userId, bool enabled, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("Account not found.");

        user.AlertsEnabled = enabled;
        await _users.UpdateAsync(user, ct);
    }

    public async Task<AuthResult> ChangePasswordAsync(
        Guid userId, ChangePasswordRequest request, string? userAgent, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            throw new ValidationException("Password must be at least 8 characters.");

        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("Account not found.");

        // Google-created accounts have a random hash the user never knew — they
        // set their first password through the reset flow, not here.
        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new ValidationException("Current password is incorrect.");

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        await _users.UpdateAsync(user, ct);

        // Sign every session out, then open a fresh one for THIS client so the
        // caller stays signed in while all other devices are dropped.
        await _refreshTokens.RevokeAllForUserAsync(userId, DateTime.UtcNow, ct);
        return await OpenSessionAsync(user, userAgent, ct);
    }

    public async Task DeleteAccountAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("Account not found.");

        // Stop billing before the account disappears — otherwise a deleted Pro
        // user keeps getting charged with no account left to manage it. Best-effort
        // (logs, never throws) so a Stripe outage can't block the user's deletion.
        await _billing.CancelSubscriptionAsync(userId, ct);

        // GDPR: unlink search history first, then remove the account. The user's
        // refresh tokens are cascade-deleted with the account row.
        await _analytics.DetachUserAsync(userId, ct);
        await _users.DeleteAsync(user, ct);
    }

    // ── Session helpers ──────────────────────────────────────────────────

    // A fresh login/registration opens a new family (a new session/device).
    private async Task<AuthResult> OpenSessionAsync(User user, string? userAgent, CancellationToken ct)
    {
        var (token, raw) = NewRefreshToken(user.Id, Guid.NewGuid(), userAgent);
        await _refreshTokens.AddAsync(token, ct);
        return BuildResult(user, raw);
    }

    private static (RefreshToken Entity, string Raw) NewRefreshToken(Guid userId, Guid familyId, string? userAgent)
    {
        // Hex is cookie-safe (no +/=). 64 random bytes → 128 hex chars.
        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(64));
        var now = DateTime.UtcNow;

        var entity = new RefreshToken
        {
            UserId = userId,
            TokenHash = HashToken(raw),
            FamilyId = familyId,
            CreatedAt = now,
            ExpiresAt = now.Add(RefreshTokenLifetime),
            UserAgent = userAgent is { Length: > 256 } ? userAgent[..256] : userAgent,
        };

        return (entity, raw);
    }

    private AuthResult BuildResult(User user, string rawRefreshToken)
    {
        var access = _tokens.CreateAccessToken(user);

        var response = new AuthResponse(
            AccessToken: access.Token,
            AccessTokenExpiry: access.Expiry,
            Email: user.Email,
            Plan: user.Plan.ToString());

        return new AuthResult(response, rawRefreshToken);
    }

    // Only the hash of a refresh token is ever stored, so a DB leak yields nothing
    // usable. SHA-256 (not a slow KDF) is right here: the token is 64 bytes of
    // CSPRNG output, not a low-entropy password, so there's nothing to brute-force.
    private static string HashToken(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    // ── Email helpers ────────────────────────────────────────────────────

    private static string IssueConfirmationToken(User user)
    {
        var now = DateTime.UtcNow;

        // Reset the rolling-window counter when the window has passed
        if (user.LastConfirmationEmailAt is null || now - user.LastConfirmationEmailAt > EmailWindow)
            user.ConfirmationEmailCount = 0;

        user.EmailConfirmationToken = NewToken();
        user.EmailConfirmationTokenExpiry = now.Add(ConfirmationTokenLifetime);
        user.LastConfirmationEmailAt = now;
        user.ConfirmationEmailCount++;

        return user.EmailConfirmationToken;
    }

    // 60s between emails, max 5 per rolling hour — per account, on top of IP rate
    // limits. Returns true when the send should be silently skipped. This must NOT
    // throw: resend-confirmation and forgot-password are enumeration-safe endpoints
    // that always answer 200, so a distinguishable 429 for a registered address
    // (vs 200 for an unknown one) would itself reveal which emails have accounts.
    private static bool IsEmailThrottled(DateTime? lastAt, int count)
    {
        if (lastAt is null) return false;

        var elapsed = DateTime.UtcNow - lastAt.Value;

        if (elapsed < EmailCooldown) return true;
        if (elapsed < EmailWindow && count >= MaxEmailsPerWindow) return true;

        return false;
    }

    private static string NewToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private string ConfirmUrl(string token) =>
        $"{_frontendUrl}/auth/confirm-email?token={token}";
}
