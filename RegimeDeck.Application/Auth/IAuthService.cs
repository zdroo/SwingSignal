using RegimeDeck.Contracts.Auth;

namespace RegimeDeck.Application.Auth;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default);
    Task<AuthResponse> GoogleLoginAsync(GoogleLoginRequest request, CancellationToken ct = default);

    Task ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct = default);
    Task ResendConfirmationAsync(ResendConfirmationRequest request, CancellationToken ct = default);
    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);

    Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken ct = default);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default);
    Task DeleteAccountAsync(Guid userId, CancellationToken ct = default);
    Task SetWeeklyReportAsync(Guid userId, bool enabled, CancellationToken ct = default);
    Task SetAlertsAsync(Guid userId, bool enabled, CancellationToken ct = default);
}
