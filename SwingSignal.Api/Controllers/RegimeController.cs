using Microsoft.AspNetCore.Mvc;
using SwingSignal.Api.Extensions;
using SwingSignal.Application.Abstractions.Ingestion;
using SwingSignal.Application.Analytics;
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
    private readonly ISearchLogService _searchLog;

    public RegimeController(
        IMacroRegimeService regime,
        IHistoricalOddsService odds,
        IAssetIngestionService ingestion,
        ISearchLogService searchLog)
    {
        _regime    = regime;
        _odds      = odds;
        _ingestion = ingestion;
        _searchLog = searchLog;
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(CancellationToken ct) =>
        Ok(await _regime.GetCurrentRegimeAsync(ct));

    [HttpGet("matches")]
    public async Task<IActionResult> GetMatches([FromQuery] int topK = 10, CancellationToken ct = default)
    {
        // Cap matches the analog count the odds engine itself uses
        if (topK is < 1 or > MatchingOptions.AnalogCount)
            return BadRequest($"topK must be between 1 and {MatchingOptions.AnalogCount}");

        return Ok(await _regime.FindSimilarPeriodsAsync(topK, ct: ct));
    }

    [HttpGet("odds/{symbol}")]
    public async Task<IActionResult> GetOdds(
        string symbol,
        [FromQuery] string? q = null,
        [FromQuery] string? src = null,
        CancellationToken ct = default)
    {
        var normalized = SymbolNormalizer.Normalize(symbol);
        var userId = User.GetUserId();

        if (!FlagshipSymbols.Contains(normalized) && User.Identity?.IsAuthenticated != true)
        {
            await _searchLog.LogAsync(normalized, q, src, userId, wasGated: true, ct);
            return Unauthorized("Create a free account to analyze any symbol.");
        }

        await _searchLog.LogAsync(normalized, q, src, userId, wasGated: false, ct);

        var asset = await _ingestion.EnsureIngestedAsync(normalized, ct);
        if (asset is null)
            return StatusCode(503, $"Could not fetch data for symbol '{normalized}'. The symbol may not be supported.");

        try
        {
            return Ok(await _odds.GetOddsAsync(normalized, ct));
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
        CancellationToken ct = default)
    {
        if (days is < 7 or > 365)
            return BadRequest("days must be between 7 and 365");

        // Custom windows are an account feature regardless of symbol
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized("Create a free account to use custom prediction windows.");

        var normalized = SymbolNormalizer.Normalize(symbol);

        var asset = await _ingestion.EnsureIngestedAsync(normalized, ct);
        if (asset is null)
            return StatusCode(503, $"Could not fetch data for symbol '{normalized}'. The symbol may not be supported.");

        try
        {
            return Ok(await _odds.GetOddsForDaysAsync(normalized, days, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
