using RegimeDeck.Contracts.Auth;

namespace RegimeDeck.Application.Auth;

public interface IAuthService
{
    // userAgent is a best-effort device label stored with the session (never trusted).
    Task<AuthResult> RegisterAsync(RegisterRequest request, string? userAgent, CancellationToken ct = default);
    Task<AuthResult> LoginAsync(LoginRequest request, string? userAgent, CancellationToken ct = default);
    Task<AuthResult> GoogleLoginAsync(GoogleLoginRequest request, string? userAgent, CancellationToken ct = default);

    /// Rotates the presented refresh token and mints a fresh access token.
    Task<AuthResult> RefreshAsync(string? refreshToken, string? userAgent, CancellationToken ct = default);

    /// Revokes the session the presented refresh token belongs to. Idempotent.
    Task LogoutAsync(string? refreshToken, CancellationToken ct = default);

    Task ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct = default);
    Task ResendConfirmationAsync(ResendConfirmationRequest request, CancellationToken ct = default);
    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);

    Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken ct = default);

    /// Changes the password, signs every OTHER session out, and returns a fresh
    /// session for the current client so it stays signed in.
    Task<AuthResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, string? userAgent, CancellationToken ct = default);

    Task DeleteAccountAsync(Guid userId, CancellationToken ct = default);
    Task SetWeeklyReportAsync(Guid userId, bool enabled, CancellationToken ct = default);
    Task SetAlertsAsync(Guid userId, bool enabled, CancellationToken ct = default);
}
