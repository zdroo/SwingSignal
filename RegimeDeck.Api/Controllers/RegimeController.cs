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
    private readonly IRegimePlaybookService _playbooks;
    private readonly IHistoricalOddsService _odds;
    private readonly IAssetIngestionService _ingestion;
    private readonly ISearchLogService _searchLog;

    public RegimeController(
        IMacroRegimeService regime,
        IRegimePlaybookService playbooks,
        IHistoricalOddsService odds,
        IAssetIngestionService ingestion,
        ISearchLogService searchLog)
    {
        _regime    = regime;
        _playbooks = playbooks;
        _odds      = odds;
        _ingestion = ingestion;
        _searchLog = searchLog;
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(CancellationToken ct) =>
        Ok(await _regime.GetCurrentRegimeAsync(ct));

    [HttpGet("playbooks")]
    public async Task<IActionResult> GetPlaybooks(CancellationToken ct) =>
        Ok(await _playbooks.GetBoardAsync(ct));

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
}
