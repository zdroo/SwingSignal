using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwingSignal.Api.Extensions;
using SwingSignal.Application.Abstractions.Ingestion;
using SwingSignal.Application.Backtesting;
using SwingSignal.Application.Common;

namespace SwingSignal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // standard backtests stay a free-account feature — the honesty proof
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("compute")]
public class BacktestController : ControllerBase
{
    private readonly IBacktestService _backtest;
    private readonly IAssetIngestionService _ingestion;
    private readonly ProFeatures _pro;

    public BacktestController(IBacktestService backtest, IAssetIngestionService ingestion, ProFeatures pro)
    {
        _backtest = backtest;
        _ingestion = ingestion;
        _pro = pro;
    }

    [HttpGet("{symbol}")]
    public async Task<IActionResult> Run(
        string symbol,
        [FromQuery] int days = 90,
        [FromQuery] int topK = 10,
        [FromQuery] int? fromYear = null,
        [FromQuery] int? toYear = null,
        [FromQuery] double? stateH = null,
        [FromQuery] string? profile = null,
        [FromQuery] bool? floorHistory = null,
        [FromQuery] double? cycleH = null,
        [FromQuery] double? shrinkM = null,
        [FromQuery] int? baseRateYears = null,
        CancellationToken ct = default)
    {
        if (days is < 7 or > 365)
            return BadRequest("days must be between 7 and 365");

        if (topK is < 1 or > 20)
            return BadRequest("topK must be between 1 and 20");

        if (profile is not null and not "crypto" and not "default" and not "crypto-native")
            return BadRequest("profile must be 'crypto', 'crypto-native' or 'default'");

        // Research knobs beyond the standard run become Pro once the flag
        // is on; the default call (days + topK) stays free forever
        var hasResearchParams = fromYear is not null || toYear is not null || stateH is not null
            || profile is not null || floorHistory is not null || cycleH is not null
            || shrinkM is not null || baseRateYears is not null;
        if (hasResearchParams)
            _pro.RequirePro(User.IsPro(), "Custom backtest parameters are a Pro feature.");

        var normalized = SymbolNormalizer.Normalize(symbol);

        var asset = await _ingestion.EnsureIngestedAsync(normalized, ct);
        if (asset is null)
            return StatusCode(503, $"Could not fetch data for symbol '{normalized}'.");

        return Ok(await _backtest.RunAsync(normalized, days, topK, fromYear, toYear, stateH, profile, floorHistory, cycleH, shrinkM, baseRateYears, ct));
    }

    // Runs the backtest twice — naive baseline vs current algorithm — to
    // measure whether the matching improvements actually help.
    [HttpGet("{symbol}/compare")]
    public async Task<IActionResult> Compare(
        string symbol,
        [FromQuery] int days = 90,
        [FromQuery] int topK = 10,
        CancellationToken ct = default)
    {
        if (days is < 7 or > 365)
            return BadRequest("days must be between 7 and 365");

        if (topK is < 1 or > 20)
            return BadRequest("topK must be between 1 and 20");

        var normalized = SymbolNormalizer.Normalize(symbol);

        var asset = await _ingestion.EnsureIngestedAsync(normalized, ct);
        if (asset is null)
            return StatusCode(503, $"Could not fetch data for symbol '{normalized}'.");

        return Ok(await _backtest.CompareAsync(normalized, days, topK, null, null, ct));
    }
}
