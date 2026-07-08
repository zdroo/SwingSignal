using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using SwingSignal.Application.Abstractions.Email;
using SwingSignal.Application.Abstractions.Persistence;
using SwingSignal.Application.Abstractions.Security;
using SwingSignal.Contracts.Auth;
using SwingSignal.Domain.Entities;

namespace SwingSignal.Application.Auth;

public class AuthService : IAuthService
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);
    private static readonly TimeSpan ConfirmationTokenLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromHours(1);

    // Per-user email throttling: minimum gap between sends, max sends per rolling hour
    private static readonly TimeSpan EmailCooldown = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan EmailWindow = TimeSpan.FromHours(1);
    private const int MaxEmailsPerWindow = 5;

    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokens;
    private readonly IGoogleTokenValidator _google;
    private readonly IEmailService _email;
    private readonly IAnalyticsRepository _analytics;
    private readonly string _frontendUrl;

    public AuthService(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        ITokenService tokens,
        IGoogleTokenValidator google,
        IEmailService email,
        IAnalyticsRepository analytics,
        IConfiguration config)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _tokens = tokens;
        _google = google;
        _email = email;
        _analytics = analytics;
        _frontendUrl = config["Frontend:Url"] ?? "http://localhost:3000";
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || email.Length > 256)
            throw new ArgumentException("A valid email address is required.");

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters.");

        if (await _users.ExistsByEmailAsync(email, ct))
            throw new InvalidOperationException("An account with this email already exists.");

        var user = new User
        {
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            CreatedAt = DateTime.UtcNow
        };

        IssueRefreshToken(user);
        var confirmToken = IssueConfirmationToken(user);
        await _users.AddAsync(user, ct);

        // Fire-and-forget semantics: a mail outage must not block registration
        await _email.SendEmailConfirmationAsync(user.Email, ConfirmUrl(confirmToken), ct);

        return BuildResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _users.GetByEmailAsync(email, ct);
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        IssueRefreshToken(user);
        await _users.UpdateAsync(user, ct);

        return BuildResponse(user);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            throw new UnauthorizedAccessException("Refresh token required.");

        var user = await _users.GetByRefreshTokenAsync(request.RefreshToken, ct);

        if (user is null || user.RefreshTokenExpiry is null || user.RefreshTokenExpiry < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token is invalid or expired.");

        IssueRefreshToken(user);
        await _users.UpdateAsync(user, ct);

        return BuildResponse(user);
    }

    public async Task<AuthResponse> GoogleLoginAsync(GoogleLoginRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
            throw new UnauthorizedAccessException("Google ID token required.");

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

            IssueRefreshToken(user);
            await _users.AddAsync(user, ct);
            await _email.SendWelcomeAsync(user.Email, ct);
        }
        else
        {
            user.IsEmailConfirmed = true; // Google ownership proof supersedes pending confirmation
            IssueRefreshToken(user);
            await _users.UpdateAsync(user, ct);
        }

        return BuildResponse(user);
    }

    public async Task ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            throw new ArgumentException("Confirmation token required.");

        var user = await _users.GetByEmailConfirmationTokenAsync(request.Token, ct);

        if (user is null || user.EmailConfirmationTokenExpiry < DateTime.UtcNow)
            throw new InvalidOperationException("This confirmation link is invalid or has expired.");

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

        ThrowIfEmailThrottled(user.LastConfirmationEmailAt, user.ConfirmationEmailCount);

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

        ThrowIfEmailThrottled(user.LastPasswordResetEmailAt, user.PasswordResetEmailCount);

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
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters.");

        var user = await _users.GetByPasswordResetTokenAsync(request.Token, ct);

        if (user is null || user.PasswordResetTokenExpiry < DateTime.UtcNow)
            throw new InvalidOperationException("This reset link is invalid or has expired.");

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiry = null;
        // A password reset proves email ownership just as well as the confirm link
        user.IsEmailConfirmed = true;
        // Invalidate existing sessions
        IssueRefreshToken(user);

        await _users.UpdateAsync(user, ct);
    }

    public async Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new KeyNotFoundException("Account not found.");

        return new UserProfileDto(user.Email, user.Plan.ToString(), user.IsEmailConfirmed, user.CreatedAt);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters.");

        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new KeyNotFoundException("Account not found.");

        // Google-created accounts have a random hash the user never knew — they
        // set their first password through the reset flow, not here.
        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("Current password is incorrect.");

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        IssueRefreshToken(user); // invalidate other sessions
        await _users.UpdateAsync(user, ct);
    }

    public async Task DeleteAccountAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new KeyNotFoundException("Account not found.");

        // GDPR: unlink search history first, then remove the account
        await _analytics.DetachUserAsync(userId, ct);
        await _users.DeleteAsync(user, ct);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static void IssueRefreshToken(User user)
    {
        user.RefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        user.RefreshTokenExpiry = DateTime.UtcNow.Add(RefreshTokenLifetime);
    }

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

    // 60s between emails, max 5 per rolling hour — per account, on top of IP rate limits
    private static void ThrowIfEmailThrottled(DateTime? lastAt, int count)
    {
        if (lastAt is null) return;

        var elapsed = DateTime.UtcNow - lastAt.Value;

        if (elapsed < EmailCooldown)
            throw new InvalidOperationException("Please wait a minute before requesting another email.");

        if (elapsed < EmailWindow && count >= MaxEmailsPerWindow)
            throw new InvalidOperationException("Too many emails requested. Please try again in an hour.");
    }

    private static string NewToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private string ConfirmUrl(string token) =>
        $"{_frontendUrl}/auth/confirm-email?token={token}";

    private AuthResponse BuildResponse(User user)
    {
        var access = _tokens.CreateAccessToken(user);

        return new AuthResponse(
            AccessToken: access.Token,
            RefreshToken: user.RefreshToken!,
            AccessTokenExpiry: access.Expiry,
            Email: user.Email,
            Plan: user.Plan.ToString());
    }
}
