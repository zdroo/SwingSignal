using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using RegimeDeck.Api.Auth;
using RegimeDeck.Application.Auth;
using RegimeDeck.Contracts.Auth;

namespace RegimeDeck.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly string _frontendUrl;

    public AuthController(IAuthService auth, IConfiguration config)
    {
        _auth = auth;
        _frontendUrl = config["Frontend:Url"] ?? "http://localhost:3000";
    }

    [HttpPost("register")]
    [EnableRateLimiting("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct) =>
        Issue(await _auth.RegisterAsync(request, UserAgent(), ct));

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct) =>
        Issue(await _auth.LoginAsync(request, UserAgent(), ct));

    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request, CancellationToken ct) =>
        Issue(await _auth.GoogleLoginAsync(request, UserAgent(), ct));

    // The refresh token rides in the HttpOnly cookie, not the body. Reject a
    // cross-site Origin so the cookie can't be driven by a malicious page (CSRF).
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        if (!OriginAllowed())
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Cross-origin refresh rejected." });

        return Issue(await _auth.RefreshAsync(RefreshTokenCookie.Read(Request), UserAgent(), ct));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (!OriginAllowed())
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Cross-origin logout rejected." });

        await _auth.LogoutAsync(RefreshTokenCookie.Read(Request), ct);
        RefreshTokenCookie.Clear(Response);
        return Ok(new { message = "Signed out." });
    }

    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest request, CancellationToken ct)
    {
        await _auth.ConfirmEmailAsync(request, ct);
        return Ok(new { message = "Email confirmed." });
    }

    // Always returns 200 — never reveals whether an email is registered
    [HttpPost("resend-confirmation")]
    public async Task<IActionResult> ResendConfirmation([FromBody] ResendConfirmationRequest request, CancellationToken ct)
    {
        await _auth.ResendConfirmationAsync(request, ct);
        return Ok(new { message = "If that address has an unconfirmed account, a new confirmation email is on its way." });
    }

    // Always returns 200 — never reveals whether an email is registered
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        await _auth.ForgotPasswordAsync(request, ct);
        return Ok(new { message = "If that address has an account, a reset email is on its way." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        await _auth.ResetPasswordAsync(request, ct);
        return Ok(new { message = "Password updated. You can now log in." });
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new
        {
            email = User.FindFirst("email")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
            plan = User.FindFirst("plan")?.Value ?? "Free"
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    // Refresh token → HttpOnly cookie; only the access token/profile goes in the body
    private OkObjectResult Issue(AuthResult result)
    {
        RefreshTokenCookie.Set(Response, result.RefreshToken);
        return Ok(result.Response);
    }

    private string? UserAgent() => Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;

    // CSRF guard for the cookie-authenticated endpoints. A browser always sends
    // Origin on a cross-origin POST; if present it must match our frontend. Absent
    // Origin (same-origin or a non-browser client) is allowed — SameSite still guards.
    private bool OriginAllowed()
    {
        var origin = Request.Headers.Origin.ToString();
        return string.IsNullOrEmpty(origin)
            || string.Equals(origin.TrimEnd('/'), _frontendUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
    }
}
