using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SwingSignal.Application.Abstractions.Persistence;
using SwingSignal.Application.Auth;
using SwingSignal.Contracts.Auth;

namespace SwingSignal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[EnableRateLimiting("auth")]
public class UsersController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IAnalyticsRepository _analytics;

    public UsersController(IAuthService auth, IAnalyticsRepository analytics)
    {
        _auth = auth;
        _analytics = analytics;
    }

    private Guid CurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var id)
            ? id
            : throw new UnauthorizedAccessException("Invalid token.");
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        try
        {
            return Ok(await _auth.GetProfileAsync(CurrentUserId(), ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPut("me/password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        try
        {
            await _auth.ChangePasswordAsync(CurrentUserId(), request, ct);
            return Ok(new { message = "Password updated. Other sessions have been signed out." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequest(ex.Message); // wrong current password — not an auth failure
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpDelete("me")]
    public async Task<IActionResult> DeleteAccount(CancellationToken ct)
    {
        var userId = CurrentUserId();

        try
        {
            // GDPR: unlink search history first, then remove the account
            await _analytics.DetachUserAsync(userId, ct);
            await _auth.DeleteAccountAsync(userId, ct);
            return Ok(new { message = "Your account and personal data have been deleted." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
