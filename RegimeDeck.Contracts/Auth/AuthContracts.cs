namespace RegimeDeck.Contracts.Auth;

public record RegisterRequest(string Email, string Password);

public record LoginRequest(string Email, string Password);

public record GoogleLoginRequest(string IdToken);

public record ConfirmEmailRequest(string Token);

public record ResendConfirmationRequest(string Email);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Token, string NewPassword);

public record WaitlistRequest(string Email, string? Source);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record UserProfileDto(string Email, string Plan, bool IsEmailConfirmed, DateTime CreatedAt, bool WeeklyReportEnabled, bool AlertsEnabled, bool HasBilling);

// The refresh token is deliberately absent: it never travels in a response body,
// only in an HttpOnly cookie the browser's JavaScript cannot read.
public record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiry,
    string Email,
    string Plan);

// Service-to-controller carrier: the JSON the client sees (Response) plus the raw
// refresh token the controller writes into the HttpOnly cookie.
public record AuthResult(AuthResponse Response, string RefreshToken);
