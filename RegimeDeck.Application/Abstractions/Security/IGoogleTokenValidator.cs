namespace RegimeDeck.Application.Abstractions.Security;

public record GoogleUserInfo(string Email, string? Name);

public interface IGoogleTokenValidator
{
    /// Validates a Google ID token and returns the verified user info.
    /// Throws UnauthorizedAccessException when the token is invalid.
    Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken ct = default);
}
