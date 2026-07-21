using Microsoft.Extensions.Configuration;
using Moq;
using RegimeDeck.Application.Abstractions.Billing;
using RegimeDeck.Application.Abstractions.Email;
using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Application.Abstractions.Security;
using RegimeDeck.Application.Auth;
using RegimeDeck.Application.Common;
using RegimeDeck.Contracts.Auth;
using RegimeDeck.Domain.Entities;
using RegimeDeck.Domain.Enums;

namespace RegimeDeck.Tests.Auth;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly Mock<IGoogleTokenValidator> _google = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<IAnalyticsRepository> _analytics = new();
    private readonly Mock<IBillingService> _billing = new();

    private const string Agent = "integration-agent";

    private AuthService BuildService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Frontend:Url"] = "http://localhost:3000",
            })
            .Build();

        _tokens.Setup(t => t.CreateAccessToken(It.IsAny<User>()))
            .Returns(new AccessToken("jwt-token", new DateTime(2026, 8, 1)));

        return new AuthService(
            _users.Object, _refreshTokens.Object, _hasher.Object, _tokens.Object, _google.Object,
            _email.Object, _analytics.Object, _billing.Object, config);
    }

    [Fact]
    public async Task DeleteAccount_CancelsBillingDetachesHistoryThenDeletesUser()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "gone@example.com" };
        _users.Setup(u => u.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Ordering: cancel billing and unlink history BEFORE the account row
        // disappears. Cancel-first stops charges; detach-before-delete is the
        // GDPR requirement.
        var calls = new List<string>();
        _billing.Setup(b => b.CancelSubscriptionAsync(userId, It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("cancel"))
            .Returns(Task.CompletedTask);
        _analytics.Setup(a => a.DetachUserAsync(userId, It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("detach"))
            .Returns(Task.CompletedTask);
        _users.Setup(u => u.DeleteAsync(user, It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("delete"))
            .Returns(Task.CompletedTask);

        await BuildService().DeleteAccountAsync(userId);

        Assert.Equal(["cancel", "detach", "delete"], calls);
    }

    [Fact]
    public async Task DeleteAccount_UnknownUser_ThrowsAndTouchesNothing()
    {
        var userId = Guid.NewGuid();
        _users.Setup(u => u.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => BuildService().DeleteAccountAsync(userId));

        _analytics.Verify(a => a.DetachUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _users.Verify(u => u.DeleteAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Register ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_PasswordTooShort_ThrowsAndCreatesNothing()
    {
        var service = BuildService();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.RegisterAsync(new RegisterRequest("user@example.com", "short"), Agent));

        Assert.Equal("Password must be at least 8 characters.", ex.Message);
        _users.Verify(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never());
        _email.Verify(e => e.SendEmailConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Register_DuplicateEmail_ThrowsAndSendsNoEmail()
    {
        _users.Setup(u => u.ExistsByEmailAsync("taken@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = BuildService();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.RegisterAsync(new RegisterRequest("taken@example.com", "password123"), Agent));

        Assert.Equal("An account with this email already exists.", ex.Message);
        _email.Verify(e => e.SendEmailConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Register_Success_CreatesUnconfirmedUserSendsConfirmationAndOpensSession()
    {
        _users.Setup(u => u.ExistsByEmailAsync("new@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _hasher.Setup(h => h.Hash("password123")).Returns("HASHED");

        User? saved = null;
        _users.Setup(u => u.AddAsync(It.Is<User>(x => x.Email == "new@example.com"), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => saved = u)
            .Returns(Task.CompletedTask);

        var service = BuildService();

        // Email is normalized to lowercase
        var result = await service.RegisterAsync(new RegisterRequest("New@Example.COM", "password123"), Agent);

        Assert.Equal("jwt-token", result.Response.AccessToken);
        Assert.Equal("new@example.com", result.Response.Email);
        Assert.Equal("Free", result.Response.Plan);
        Assert.False(string.IsNullOrEmpty(result.RefreshToken)); // raw refresh token minted for the cookie

        Assert.NotNull(saved);
        Assert.Equal("HASHED", saved.PasswordHash);
        Assert.False(saved.IsEmailConfirmed);
        Assert.Equal(1, saved.ConfirmationEmailCount);
        Assert.NotNull(saved.EmailConfirmationToken);

        // A session row was opened for this user (new family)
        _refreshTokens.Verify(r => r.AddAsync(
            It.Is<RefreshToken>(t => t.UserId == saved!.Id && !string.IsNullOrEmpty(t.TokenHash)),
            It.IsAny<CancellationToken>()), Times.Once());

        _email.Verify(e => e.SendEmailConfirmationAsync(
            "new@example.com",
            $"http://localhost:3000/auth/confirm-email?token={saved.EmailConfirmationToken}",
            It.IsAny<CancellationToken>()), Times.Once());
    }

    // ── Login ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_WrongPassword_ThrowsUnauthorized()
    {
        var user = new User { Email = "user@example.com", PasswordHash = "STORED" };
        _users.Setup(u => u.GetByEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("wrongpass", "STORED")).Returns(false);

        var service = BuildService();

        var ex = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            service.LoginAsync(new LoginRequest("user@example.com", "wrongpass"), Agent));

        Assert.Equal("Invalid email or password.", ex.Message);
        _refreshTokens.Verify(r => r.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Login_CorrectPassword_OpensASession()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "user@example.com", PasswordHash = "STORED", Plan = UserPlan.Free };
        // Login normalizes the address, so the mixed-case input must still resolve
        _users.Setup(u => u.GetByEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("rightpass", "STORED")).Returns(true);

        var service = BuildService();

        var result = await service.LoginAsync(new LoginRequest("User@Example.COM", "rightpass"), Agent);

        Assert.Equal("jwt-token", result.Response.AccessToken);
        Assert.Equal("user@example.com", result.Response.Email);
        Assert.False(string.IsNullOrEmpty(result.RefreshToken));
        // A new session (family) was opened for this user
        _refreshTokens.Verify(r => r.AddAsync(
            It.Is<RefreshToken>(t => t.UserId == user.Id), It.IsAny<CancellationToken>()), Times.Once());
    }

    // ── Refresh: rotation & reuse detection ──────────────────────────────

    [Fact]
    public async Task Refresh_ValidToken_RotatesWithinTheSameFamily()
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "user@example.com", Plan = UserPlan.Free };
        var current = new RefreshToken
        {
            UserId = userId,
            FamilyId = familyId,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
        };
        // The service hashes whatever raw token it's given, then looks it up — so
        // return `current` for ANY hash lookup in this test.
        _refreshTokens.Setup(r => r.GetByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);
        _users.Setup(u => u.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        RefreshToken? replacement = null;
        _refreshTokens.Setup(r => r.RotateAsync(current, It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Callback<RefreshToken, RefreshToken, CancellationToken>((_, repl, _) => replacement = repl)
            .Returns(Task.CompletedTask);

        var result = await BuildService().RefreshAsync("some-raw-token", Agent);

        Assert.Equal("jwt-token", result.Response.AccessToken);
        Assert.False(string.IsNullOrEmpty(result.RefreshToken));
        Assert.NotNull(replacement);
        Assert.Equal(familyId, replacement.FamilyId);   // stays in the same rotation chain
        Assert.Equal(userId, replacement.UserId);
    }

    [Fact]
    public async Task Refresh_RevokedTokenReplayed_RevokesTheWholeFamilyAndThrows()
    {
        var familyId = Guid.NewGuid();
        var revoked = new RefreshToken
        {
            UserId = Guid.NewGuid(),
            FamilyId = familyId,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            RevokedAt = DateTime.UtcNow.AddMinutes(-1), // already rotated away → reuse = theft
        };
        _refreshTokens.Setup(r => r.GetByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(revoked);

        var service = BuildService();

        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            service.RefreshAsync("replayed-token", Agent));

        _refreshTokens.Verify(r => r.RevokeFamilyAsync(familyId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once());
        _refreshTokens.Verify(r => r.RotateAsync(It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Refresh_ExpiredToken_Throws()
    {
        var expired = new RefreshToken
        {
            UserId = Guid.NewGuid(),
            FamilyId = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
        };
        _refreshTokens.Setup(r => r.GetByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expired);

        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            BuildService().RefreshAsync("expired-token", Agent));
    }

    [Fact]
    public async Task Refresh_UnknownOrEmptyToken_Throws()
    {
        _refreshTokens.Setup(r => r.GetByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);
        var service = BuildService();

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => service.RefreshAsync("nope", Agent));
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => service.RefreshAsync(null, Agent));
    }

    // ── Logout ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_RevokesTheFamily()
    {
        var familyId = Guid.NewGuid();
        var token = new RefreshToken { FamilyId = familyId, UserId = Guid.NewGuid(), ExpiresAt = DateTime.UtcNow.AddDays(1) };
        _refreshTokens.Setup(r => r.GetByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        await BuildService().LogoutAsync("a-token");

        _refreshTokens.Verify(r => r.RevokeFamilyAsync(familyId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Logout_NoCookie_IsANoOp()
    {
        await BuildService().LogoutAsync(null);
        _refreshTokens.Verify(r => r.RevokeFamilyAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    // ── Confirmation resend throttling ───────────────────────────────────

    // Throttling must be SILENT on this enumeration-safe endpoint: a 429 for a
    // real (throttled) account vs a 200 for an unknown email would reveal which
    // addresses are registered. So a throttled resend is a no-op, not a throw.
    [Fact]
    public async Task ResendConfirmation_WithinCooldown_SilentlySkipsWithoutThrowing()
    {
        var user = new User
        {
            Email = "user@example.com",
            IsEmailConfirmed = false,
            LastConfirmationEmailAt = DateTime.UtcNow.AddSeconds(-10),
            ConfirmationEmailCount = 1,
        };
        _users.Setup(u => u.GetByEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var service = BuildService();

        await service.ResendConfirmationAsync(new ResendConfirmationRequest("user@example.com"));

        _email.Verify(e => e.SendEmailConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
        _users.Verify(u => u.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task ResendConfirmation_UnknownEmail_SilentlyDoesNothing()
    {
        _users.Setup(u => u.GetByEmailAsync("ghost@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var service = BuildService();

        await service.ResendConfirmationAsync(new ResendConfirmationRequest("ghost@example.com"));

        _email.Verify(e => e.SendEmailConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
        _users.Verify(u => u.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    // ── Password reset ───────────────────────────────────────────────────

    [Fact]
    public async Task ResetPassword_ExpiredToken_Throws()
    {
        var user = new User
        {
            Email = "user@example.com",
            PasswordResetToken = "TOKEN123",
            PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(-1),
        };
        _users.Setup(u => u.GetByPasswordResetTokenAsync("TOKEN123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var service = BuildService();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.ResetPasswordAsync(new ResetPasswordRequest("TOKEN123", "newpassword1")));

        Assert.Equal("This reset link is invalid or has expired.", ex.Message);
    }

    [Fact]
    public async Task ResetPassword_ValidToken_UpdatesHashClearsTokenConfirmsEmailAndRevokesAllSessions()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "user@example.com",
            PasswordHash = "OLD",
            IsEmailConfirmed = false,
            PasswordResetToken = "TOKEN123",
            PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(30),
        };
        _users.Setup(u => u.GetByPasswordResetTokenAsync("TOKEN123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _hasher.Setup(h => h.Hash("newpassword1")).Returns("NEWHASH");

        var service = BuildService();

        await service.ResetPasswordAsync(new ResetPasswordRequest("TOKEN123", "newpassword1"));

        Assert.Equal("NEWHASH", user.PasswordHash);
        Assert.Null(user.PasswordResetToken);
        Assert.Null(user.PasswordResetTokenExpiry);
        Assert.True(user.IsEmailConfirmed);        // reset proves email ownership
        _users.Verify(u => u.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once());
        // Every existing session is invalidated
        _refreshTokens.Verify(r => r.RevokeAllForUserAsync(userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    // Regression: a reset request carrying no token must be rejected outright.
    // Without the guard, EF Core's `col == null` → `col IS NULL` would match the
    // first user with no pending reset and let an attacker take over their account.
    [Fact]
    public async Task ResetPassword_NullToken_ThrowsAndNeverLooksUpAUser()
    {
        var service = BuildService();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.ResetPasswordAsync(new ResetPasswordRequest(null!, "newpassword1")));

        Assert.Equal("Reset token required.", ex.Message);
        _users.Verify(u => u.GetByPasswordResetTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task ResetPassword_BlankToken_ThrowsBeforeAnyLookup()
    {
        var service = BuildService();

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.ResetPasswordAsync(new ResetPasswordRequest("   ", "newpassword1")));

        _users.Verify(u => u.GetByPasswordResetTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    // ── Change password ──────────────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_Valid_RevokesAllThenOpensAFreshSession()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "user@example.com", PasswordHash = "OLD" };
        _users.Setup(u => u.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("current1", "OLD")).Returns(true);
        _hasher.Setup(h => h.Hash("newpassword1")).Returns("NEWHASH");

        var calls = new List<string>();
        _refreshTokens.Setup(r => r.RevokeAllForUserAsync(userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("revoke-all")).Returns(Task.CompletedTask);
        _refreshTokens.Setup(r => r.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("add")).Returns(Task.CompletedTask);

        var result = await BuildService().ChangePasswordAsync(
            userId, new ChangePasswordRequest("current1", "newpassword1"), Agent);

        Assert.Equal("NEWHASH", user.PasswordHash);
        Assert.Equal("jwt-token", result.Response.AccessToken);
        Assert.False(string.IsNullOrEmpty(result.RefreshToken)); // fresh session for the current client
        // Revoke everything FIRST, then open the new session (else it'd be revoked too)
        Assert.Equal(["revoke-all", "add"], calls);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrent_Throws()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, PasswordHash = "OLD" };
        _users.Setup(u => u.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("wrong", "OLD")).Returns(false);

        await Assert.ThrowsAsync<ValidationException>(() => BuildService().ChangePasswordAsync(
            userId, new ChangePasswordRequest("wrong", "newpassword1"), Agent));

        _refreshTokens.Verify(r => r.RevokeAllForUserAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    // ── Google ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GoogleLogin_NewUser_CreatesConfirmedAccountSendsWelcomeAndOpensSession()
    {
        _google.Setup(g => g.ValidateAsync("id-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleUserInfo("googler@example.com", "G"));
        _users.Setup(u => u.GetByEmailAsync("googler@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("RANDOMHASH");

        User? saved = null;
        _users.Setup(u => u.AddAsync(It.Is<User>(x => x.Email == "googler@example.com"), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => saved = u)
            .Returns(Task.CompletedTask);

        var service = BuildService();

        var result = await service.GoogleLoginAsync(new GoogleLoginRequest("id-token"), Agent);

        Assert.Equal("googler@example.com", result.Response.Email);
        Assert.False(string.IsNullOrEmpty(result.RefreshToken));
        Assert.NotNull(saved);
        Assert.True(saved.IsEmailConfirmed); // Google already verified the address
        Assert.Equal(UserPlan.Free, saved.Plan);

        _refreshTokens.Verify(r => r.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once());
        _email.Verify(e => e.SendWelcomeAsync("googler@example.com", It.IsAny<CancellationToken>()), Times.Once());
        _email.Verify(e => e.SendEmailConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
    }
}
