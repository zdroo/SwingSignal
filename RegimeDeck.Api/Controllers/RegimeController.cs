using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RegimeDeck.Api.Extensions;
using RegimeDeck.Application.Abstractions.Ingestion;
using RegimeDeck.Application.Analytics;
using RegimeDeck.Application.Common;
using RegimeDeck.Application.Odds;
using RegimeDeck.Application.Regime;

namespace RegimeDeck.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RegimeController : ControllerBase
{
    // Free without an account (the "BTC, SPY and Gold are free" teaser). GLD is the
    // ticker shown to anonymous visitors; GC=F kept since it was already granted.
    private static readonly HashSet<string> FlagshipSymbols =
        new(StringComparer.OrdinalIgnoreCase) { "BTCUSDT", "SPY", "GLD", "GC=F" };

    private readonly IMacroRegimeService _regime;
    private readonly IHistoricalOddsService _odds;
    private readonly IAssetIngestionService _ingestion;
    private readonly ISearchLogService _searchLog;
    private readonly ProFeatures _pro;
    private readonly IDailyQuota _quota;

    public RegimeController(
        IMacroRegimeService regime,
        IHistoricalOddsService odds,
        IAssetIngestionService ingestion,
        ISearchLogService searchLog,
        ProFeatures pro,
        IDailyQuota quota)
    {
        _regime    = regime;
        _odds      = odds;
        _ingestion = ingestion;
        _searchLog = searchLog;
        _pro       = pro;
        _quota     = quota;
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

    // Can trigger external ingestion for new symbols — capped per IP
    [HttpGet("odds/{symbol}")]
    [EnableRateLimiting("odds")]
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

        _ = await _ingestion.EnsureSupportedAsync(normalized, ct);
        return Ok(await _odds.GetOddsAsync(normalized, ct));
    }

    // Re-runs the full analog computation per request — hence the strict compute budget
    [HttpGet("odds/{symbol}/period")]
    [EnableRateLimiting("compute")]
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

        // When Pro is live: Free gets a daily allowance, Pro is uncapped. No-op while the flag is off.
        if (_pro.GateActive(User.IsPro())
            && !_quota.TryConsume(User.RequireUserId(), ProFeatures.CustomWindowDailyLimit))
        {
            throw new RateLimitedException(
                $"You've used today's {ProFeatures.CustomWindowDailyLimit} custom windows. " +
                "Pro removes this cap — or come back tomorrow.");
        }

        var asset = await _ingestion.EnsureSupportedAsync(symbol, ct);
        return Ok(await _odds.GetOddsForDaysAsync(asset.Symbol, days, ct));
    }
}
