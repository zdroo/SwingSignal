using Microsoft.AspNetCore.Mvc;
using SwingSignal.Application.Interfaces;

namespace SwingSignal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RegimeController : ControllerBase
{
    private readonly IMacroRegimeService _regime;
    private readonly IHistoricalOddsService _odds;

    public RegimeController(IMacroRegimeService regime, IHistoricalOddsService odds)
    {
        _regime = regime;
        _odds = odds;
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(CancellationToken ct)
    {
        var regime = await _regime.GetCurrentRegimeAsync(ct);
        return Ok(regime);
    }

    [HttpGet("matches")]
    public async Task<IActionResult> GetMatches([FromQuery] int topK = 10, CancellationToken ct = default)
    {
        if (topK is < 1 or > 20)
            return BadRequest("topK must be between 1 and 20");

        var matches = await _regime.FindSimilarPeriodsAsync(topK, ct);
        return Ok(matches);
    }

    [HttpGet("odds/{symbol}")]
    public async Task<IActionResult> GetOdds(string symbol, [FromQuery] int topK = 10, CancellationToken ct = default)
    {
        try
        {
            var odds = await _odds.GetOddsAsync(symbol, topK, ct);
            return Ok(odds);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
