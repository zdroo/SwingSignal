using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegimeDeck.Application.Screener;

namespace RegimeDeck.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScreenerController : ControllerBase
{
    private readonly IScreenerService _screener;

    public ScreenerController(IScreenerService screener) => _screener = screener;

    // Free teaser board — anonymous, like the regime dashboard. Cheap cache
    // read, so the global limiter is enough.
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(await _screener.GetAsync(isPro: false, query: null, ct));

    // Full universe with filters — Pro only (invisible to Free/anonymous
    // until the plan claim says Pro).
    [HttpGet("full")]
    [Authorize(Policy = "ProOnly")]
    public async Task<IActionResult> Full(
        [FromQuery] string? stance,
        [FromQuery] string? market,
        [FromQuery] double? minEdge,
        CancellationToken ct) =>
        Ok(await _screener.GetAsync(isPro: true, new ScreenerQuery(stance, market, minEdge), ct));
}
