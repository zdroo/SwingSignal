using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SwingSignal.Application.Abstractions.Persistence;
using SwingSignal.Contracts.Auth;
using SwingSignal.Domain.Entities;

namespace SwingSignal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("public-sensitive")]
public class WaitlistController : ControllerBase
{
    private readonly IAnalyticsRepository _analytics;

    public WaitlistController(IAnalyticsRepository analytics) => _analytics = analytics;

    [HttpPost]
    public async Task<IActionResult> Join([FromBody] WaitlistRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || email.Length > 256)
            return BadRequest("A valid email address is required.");

        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;

        await _analytics.AddToWaitlistAsync(new WaitlistEntry
        {
            Email = email,
            UserId = Guid.TryParse(sub, out var id) ? id : null,
            Source = request.Source?[..Math.Min(request.Source.Length, 64)],
            CreatedAt = DateTime.UtcNow
        }, ct);

        // Idempotent by design: already-registered emails also get a friendly answer
        return Ok(new { message = "You're on the list — we'll email you when Pro launches." });
    }
}
