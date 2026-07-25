using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegimeDeck.Api.Extensions;
using RegimeDeck.Application.Abstractions.Ingestion;
using RegimeDeck.Application.Backtesting;
using RegimeDeck.Application.Common;
using RegimeDeck.Contracts.Backtests;

namespace RegimeDeck.Api.Controllers;

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
    public async Task<IActionResult> Run(string symbol, [FromQuery] BacktestQuery query, CancellationToken ct = default)
    {
        if (query.Validate() is { } error)
            return BadRequest(error);

        // Research knobs beyond the standard run become Pro once the flag is on;
        // the default call (days + topK) stays free forever.
        if (query.HasResearchParams)
            _pro.RequirePro(User.IsPro(), "Custom backtest parameters are a Pro feature.");

        var asset = await _ingestion.EnsureSupportedAsync(symbol, ct);
        return Ok(await _backtest.RunAsync(asset.Symbol, query, ct));
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

        var asset = await _ingestion.EnsureSupportedAsync(symbol, ct);
        return Ok(await _backtest.CompareAsync(asset.Symbol, days, topK, ct));
    }
}
