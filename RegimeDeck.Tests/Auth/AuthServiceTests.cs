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
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly Mock<IGoogleTokenValidator> _google = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<IAnalyticsRepository> _analytics = new();
    private readonly Mock<IBillingService> _billing = new();

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
            _users.Object, _hasher.Object, _tokens.Object, _google.Object, _email.Object,
            _analytics.Object, _billing.Object, config);
    }

    [Fact]
    public async Task DeleteAccount_DetachesSearchHistoryBeforeDeletingTheUser()
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
            service.RegisterAsync(new RegisterRequest("user@example.com", "short")));

        Assert.Equal("Password must be at least 8 characters.", ex.Message);
        // Verifying nothing happened — the sanctioned It.IsAny use
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
            service.RegisterAsync(new RegisterRequest("taken@example.com", "password123")));

        Assert.Equal("An account with this email already exists.", ex.Message);
        _email.Verify(e => e.SendEmailConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Register_Success_CreatesUnconfirmedUserAndSendsConfirmation()
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
        var response = await service.RegisterAsync(new RegisterRequest("New@Example.COM", "password123"));

        Assert.Equal("jwt-token", response.AccessToken);
        Assert.Equal("new@example.com", response.Email);
        Assert.Equal("Free", response.Plan);

        Assert.NotNull(saved);
        Assert.Equal("HASHED", saved.PasswordHash);
        Assert.False(saved.IsEmailConfirmed);
        Assert.Equal(1, saved.ConfirmationEmailCount);
        Assert.NotNull(saved.EmailConfirmationToken);
        Assert.NotNull(saved.RefreshToken);

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
            service.LoginAsync(new LoginRequest("user@example.com", "wrongpass")));

        Assert.Equal("Invalid email or password.", ex.Message);
        _users.Verify(u => u.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Login_CorrectPassword_IssuesTokensAndPersistsFreshRefresh()
    {
        var user = new User { Email = "user@example.com", PasswordHash = "STORED", Plan = UserPlan.Free };
        // Login normalizes the address, so the mixed-case input must still resolve
        _users.Setup(u => u.GetByEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("rightpass", "STORED")).Returns(true);

        var service = BuildService();

        var response = await service.LoginAsync(new LoginRequest("User@Example.COM", "rightpass"));

        Assert.Equal("jwt-token", response.AccessToken);
        Assert.Equal("user@example.com", response.Email);
        Assert.Equal("Free", response.Plan);
        Assert.False(string.IsNullOrEmpty(response.RefreshToken)); // a fresh refresh token was minted
        Assert.NotNull(user.RefreshTokenExpiry);
        _users.Verify(u => u.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once()); // rotation persisted
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

        // No throw — indistinguishable from the unknown-email path
        await service.ResendConfirmationAsync(new ResendConfirmationRequest("user@example.com"));

        _email.Verify(e => e.SendEmailConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
        _users.Verify(u => u.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task ResendConfirmation_WindowExhausted_SilentlySkipsWithoutThrowing()
    {
        var user = new User
        {
            Email = "user@example.com",
            IsEmailConfirmed = false,
            LastConfirmationEmailAt = DateTime.UtcNow.AddMinutes(-5),
            ConfirmationEmailCount = 5,
        };
        _users.Setup(u => u.GetByEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var service = BuildService();

        await service.ResendConfirmationAsync(new ResendConfirmationRequest("user@example.com"));

        _email.Verify(e => e.SendEmailConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
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
        _users.Verify(u => u.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task ResetPassword_BlankToken_ThrowsBeforeAnyLookup()
    {
        var service = BuildService();

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.ResetPasswordAsync(new ResetPasswordRequest("   ", "newpassword1")));

        _users.Verify(u => u.GetByPasswordResetTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task ResendConfirmation_UnknownEmail_SilentlyDoesNothing()
    {
        _users.Setup(u => u.GetByEmailAsync("ghost@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var service = BuildService();

        await service.ResendConfirmationAsync(new ResendConfirmationRequest("ghost@example.com"));

        // No user enumeration: no exception, no email, no writes
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
    public async Task ResetPassword_ValidToken_UpdatesHashClearsTokenAndConfirmsEmail()
    {
        var user = new User
        {
            Email = "user@example.com",
            PasswordHash = "OLD",
            IsEmailConfirmed = false,
            PasswordResetToken = "TOKEN123",
            PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(30),
            RefreshToken = "old-refresh",
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
        Assert.NotEqual("old-refresh", user.RefreshToken); // sessions invalidated
        _users.Verify(u => u.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once());
    }

    // ── Google ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GoogleLogin_NewUser_CreatesConfirmedAccountAndSendsWelcome()
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

        var response = await service.GoogleLoginAsync(new GoogleLoginRequest("id-token"));

        Assert.Equal("googler@example.com", response.Email);
        Assert.NotNull(saved);
        Assert.True(saved.IsEmailConfirmed); // Google already verified the address
        Assert.Equal(UserPlan.Free, saved.Plan);

        _email.Verify(e => e.SendWelcomeAsync("googler@example.com", It.IsAny<CancellationToken>()), Times.Once());
        _email.Verify(e => e.SendEmailConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
    }
}
