using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SwingSignal.Api.Extensions;
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

    public UsersController(IAuthService auth) => _auth = auth;

    [HttpGet("me")]
    public async Task<IActionResult> GetProfile(CancellationToken ct) =>
        Ok(await _auth.GetProfileAsync(User.RequireUserId(), ct));

    [HttpPut("me/password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        await _auth.ChangePasswordAsync(User.RequireUserId(), request, ct);
        return Ok(new { message = "Password updated. Other sessions have been signed out." });
    }

    public record WeeklyReportRequest(bool Enabled);

    [HttpPut("me/weekly-report")]
    public async Task<IActionResult> SetWeeklyReport([FromBody] WeeklyReportRequest request, CancellationToken ct)
    {
        await _auth.SetWeeklyReportAsync(User.RequireUserId(), request.Enabled, ct);
        return Ok(new { message = request.Enabled ? "Weekly report enabled." : "Weekly report disabled." });
    }

    [HttpDelete("me")]
    public async Task<IActionResult> DeleteAccount(CancellationToken ct)
    {
        await _auth.DeleteAccountAsync(User.RequireUserId(), ct);
        return Ok(new { message = "Your account and personal data have been deleted." });
    }
}
