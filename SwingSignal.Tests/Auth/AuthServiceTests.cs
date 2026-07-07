using Microsoft.Extensions.Configuration;
using Moq;
using SwingSignal.Application.Abstractions.Email;
using SwingSignal.Application.Abstractions.Persistence;
using SwingSignal.Application.Abstractions.Security;
using SwingSignal.Application.Auth;
using SwingSignal.Contracts.Auth;
using SwingSignal.Domain.Entities;
using SwingSignal.Domain.Enums;

namespace SwingSignal.Tests.Auth;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly Mock<IGoogleTokenValidator> _google = new();
    private readonly Mock<IEmailService> _email = new();

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

        return new AuthService(_users.Object, _hasher.Object, _tokens.Object, _google.Object, _email.Object, config);
    }

    // ── Register ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_PasswordTooShort_ThrowsAndCreatesNothing()
    {
        var service = BuildService();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
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

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
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

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LoginAsync(new LoginRequest("user@example.com", "wrongpass")));

        Assert.Equal("Invalid email or password.", ex.Message);
        _users.Verify(u => u.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    // ── Confirmation resend throttling ───────────────────────────────────

    [Fact]
    public async Task ResendConfirmation_WithinCooldown_ThrowsAndSendsNothing()
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

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ResendConfirmationAsync(new ResendConfirmationRequest("user@example.com")));

        Assert.Equal("Please wait a minute before requesting another email.", ex.Message);
        _email.Verify(e => e.SendEmailConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task ResendConfirmation_WindowExhausted_ThrowsHourlyMessage()
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

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ResendConfirmationAsync(new ResendConfirmationRequest("user@example.com")));

        Assert.Equal("Too many emails requested. Please try again in an hour.", ex.Message);
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

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
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
