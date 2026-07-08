using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwingSignal.Application.Abstractions.Ingestion;
using SwingSignal.Application.Backtesting;
using SwingSignal.Application.Common;

namespace SwingSignal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // account required; Pro-only once billing exists
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("compute")]
public class BacktestController : ControllerBase
{
    private readonly IBacktestService _backtest;
    private readonly IAssetIngestionService _ingestion;

    public BacktestController(IBacktestService backtest, IAssetIngestionService ingestion)
    {
        _backtest = backtest;
        _ingestion = ingestion;
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
        CancellationToken ct = default)
    {
        if (days is < 7 or > 365)
            return BadRequest("days must be between 7 and 365");

        if (topK is < 1 or > 20)
            return BadRequest("topK must be between 1 and 20");

        if (profile is not null and not "crypto" and not "default")
            return BadRequest("profile must be 'crypto' or 'default'");

        var normalized = SymbolNormalizer.Normalize(symbol);

        var asset = await _ingestion.EnsureIngestedAsync(normalized, ct);
        if (asset is null)
            return StatusCode(503, $"Could not fetch data for symbol '{normalized}'.");

        try
        {
            var result = await _backtest.RunAsync(normalized, days, topK, fromYear, toYear, stateH, profile, floorHistory, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
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

        try
        {
            var result = await _backtest.CompareAsync(normalized, days, topK, null, null, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
