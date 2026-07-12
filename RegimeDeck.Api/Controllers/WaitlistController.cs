using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RegimeDeck.Api.Extensions;
using RegimeDeck.Application.Analytics;
using RegimeDeck.Contracts.Auth;

namespace RegimeDeck.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("public-sensitive")]
public class WaitlistController : ControllerBase
{
    private readonly IWaitlistService _waitlist;

    public WaitlistController(IWaitlistService waitlist) => _waitlist = waitlist;

    [HttpPost]
    public async Task<IActionResult> Join([FromBody] WaitlistRequest request, CancellationToken ct)
    {
        await _waitlist.JoinAsync(request.Email, request.Source, User.GetUserId(), ct);
        return Ok(new { message = "You're on the list — we'll email you when Pro launches." });
    }
}
