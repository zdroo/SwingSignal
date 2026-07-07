using Microsoft.AspNetCore.Mvc;
using SwingSignal.Application.Abstractions.Ingestion;
using SwingSignal.Application.Common;
using SwingSignal.Application.Odds;
using SwingSignal.Application.Regime;

namespace SwingSignal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RegimeController : ControllerBase
{
    // Odds for these are free without an account — the landing page teaser tier
    private static readonly HashSet<string> FlagshipSymbols =
        new(StringComparer.OrdinalIgnoreCase) { "BTCUSDT", "SPY", "GC=F" };

    private readonly IMacroRegimeService _regime;
    private readonly IHistoricalOddsService _odds;
    private readonly IAssetIngestionService _ingestion;

    public RegimeController(
        IMacroRegimeService regime,
        IHistoricalOddsService odds,
        IAssetIngestionService ingestion)
    {
        _regime    = regime;
        _odds      = odds;
        _ingestion = ingestion;
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
        var normalized = SymbolNormalizer.Normalize(symbol);

        if (!FlagshipSymbols.Contains(normalized) && User.Identity?.IsAuthenticated != true)
            return Unauthorized("Create a free account to analyze any symbol.");

        var asset = await _ingestion.EnsureIngestedAsync(normalized, ct);
        if (asset is null)
            return StatusCode(503, $"Could not fetch data for symbol '{normalized}'. The symbol may not be supported.");

        try
        {
            var odds = await _odds.GetOddsAsync(normalized, topK, ct);
            return Ok(odds);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("odds/{symbol}/period")]
    public async Task<IActionResult> GetOddsForPeriod(
        string symbol,
        [FromQuery] int days = 30,
        [FromQuery] int topK = 10,
        CancellationToken ct = default)
    {
        if (days is < 7 or > 365)
            return BadRequest("days must be between 7 and 365");

        var normalized = SymbolNormalizer.Normalize(symbol);

        // Custom windows are an account feature regardless of symbol
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized("Create a free account to use custom prediction windows.");

        var asset = await _ingestion.EnsureIngestedAsync(normalized, ct);
        if (asset is null)
            return StatusCode(503, $"Could not fetch data for symbol '{normalized}'. The symbol may not be supported.");

        try
        {
            var odds = await _odds.GetOddsForDaysAsync(normalized, days, topK, ct);
            return Ok(odds);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
