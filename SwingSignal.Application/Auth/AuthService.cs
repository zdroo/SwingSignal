using System.Security.Cryptography;
using SwingSignal.Application.Abstractions.Persistence;
using SwingSignal.Application.Abstractions.Security;
using SwingSignal.Contracts.Auth;
using SwingSignal.Domain.Entities;

namespace SwingSignal.Application.Auth;

public class AuthService : IAuthService
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokens;
    private readonly IGoogleTokenValidator _google;

    public AuthService(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        ITokenService tokens,
        IGoogleTokenValidator google)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _tokens = tokens;
        _google = google;
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
        await _users.AddAsync(user, ct);

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
            // First Google sign-in: create the account. The random password hash
            // means password login stays impossible until the user sets one.
            user = new User
            {
                Email = email,
                PasswordHash = _passwordHasher.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),
                CreatedAt = DateTime.UtcNow
            };

            IssueRefreshToken(user);
            await _users.AddAsync(user, ct);
        }
        else
        {
            IssueRefreshToken(user);
            await _users.UpdateAsync(user, ct);
        }

        return BuildResponse(user);
    }

    private static void IssueRefreshToken(User user)
    {
        user.RefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        user.RefreshTokenExpiry = DateTime.UtcNow.Add(RefreshTokenLifetime);
    }

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
