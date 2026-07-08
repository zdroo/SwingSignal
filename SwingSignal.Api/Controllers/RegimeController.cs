using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SwingSignal.Application.Abstractions.Ingestion;
using SwingSignal.Application.Abstractions.Persistence;
using SwingSignal.Application.Common;
using SwingSignal.Application.Odds;
using SwingSignal.Application.Regime;
using SwingSignal.Domain.Entities;

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
    private readonly IAnalyticsRepository _analytics;
    private readonly ILogger<RegimeController> _logger;

    public RegimeController(
        IMacroRegimeService regime,
        IHistoricalOddsService odds,
        IAssetIngestionService ingestion,
        IAnalyticsRepository analytics,
        ILogger<RegimeController> logger)
    {
        _regime    = regime;
        _odds      = odds;
        _ingestion = ingestion;
        _analytics = analytics;
        _logger    = logger;
    }

    private Guid? CurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    // Best-effort demand logging — must never fail the request
    private async Task LogSearchAsync(string symbol, string? rawQuery, string? source, bool wasGated, CancellationToken ct)
    {
        try
        {
            await _analytics.LogSearchAsync(new SearchLog
            {
                Symbol = symbol,
                RawQuery = string.IsNullOrWhiteSpace(rawQuery) ? null : rawQuery[..Math.Min(rawQuery.Length, 200)],
                Source = string.IsNullOrWhiteSpace(source) ? "direct" : source,
                UserId = CurrentUserId(),
                WasGated = wasGated,
                CreatedAt = DateTime.UtcNow
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Search logging failed for {Symbol}", symbol);
        }
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
        // Cap matches the analog count the odds engine itself uses
        if (topK is < 1 or > MatchingOptions.AnalogCount)
            return BadRequest($"topK must be between 1 and {MatchingOptions.AnalogCount}");

        var matches = await _regime.FindSimilarPeriodsAsync(topK, ct: ct);
        return Ok(matches);
    }

    [HttpGet("odds/{symbol}")]
    public async Task<IActionResult> GetOdds(
        string symbol,
        [FromQuery] int topK = 10,
        [FromQuery] string? q = null,
        [FromQuery] string? src = null,
        CancellationToken ct = default)
    {
        var normalized = SymbolNormalizer.Normalize(symbol);

        if (!FlagshipSymbols.Contains(normalized) && User.Identity?.IsAuthenticated != true)
        {
            await LogSearchAsync(normalized, q, src, wasGated: true, ct);
            return Unauthorized("Create a free account to analyze any symbol.");
        }

        await LogSearchAsync(normalized, q, src, wasGated: false, ct);

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
