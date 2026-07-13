using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RegimeDeck.Api.Extensions;
using RegimeDeck.Application.Watchlist;
using RegimeDeck.Contracts.Watchlist;

namespace RegimeDeck.Api.Controllers;

// Pro from birth — a new feature, so the free tier loses nothing. While the
// dark-launch flag is off, only manually upgraded accounts can reach this.
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ProOnly")]
public class WatchlistController : ControllerBase
{
    private readonly IWatchlistService _watchlist;

    public WatchlistController(IWatchlistService watchlist) => _watchlist = watchlist;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(await _watchlist.GetAsync(User.RequireUserId(), ct));

    /// The list with each asset's current statistical picture — one odds
    /// computation per row, hence the compute rate limit.
    [HttpGet("overview")]
    [EnableRateLimiting("compute")]
    public async Task<IActionResult> Overview(CancellationToken ct) =>
        Ok(await _watchlist.GetOverviewAsync(User.RequireUserId(), ct));

    // Can trigger external ingestion for new symbols — same cap as the odds endpoint
    [HttpPost]
    [EnableRateLimiting("odds")]
    public async Task<IActionResult> Add([FromBody] AddWatchlistRequest request, CancellationToken ct) =>
        Ok(await _watchlist.AddAsync(User.RequireUserId(), request.Symbol, ct));

    [HttpDelete("{symbol}")]
    public async Task<IActionResult> Remove(string symbol, CancellationToken ct)
    {
        await _watchlist.RemoveAsync(User.RequireUserId(), symbol, ct);
        return NoContent();
    }
}
