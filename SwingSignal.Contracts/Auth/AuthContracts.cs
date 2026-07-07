namespace SwingSignal.Contracts.Auth;

public record RegisterRequest(string Email, string Password);

public record LoginRequest(string Email, string Password);

public record RefreshRequest(string RefreshToken);

public record GoogleLoginRequest(string IdToken);

public record ConfirmEmailRequest(string Token);

public record ResendConfirmationRequest(string Email);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Token, string NewPassword);

public record WaitlistRequest(string Email, string? Source);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiry,
    string Email,
    string Plan);
