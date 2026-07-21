using Microsoft.AspNetCore.Mvc;
using RegimeDeck.Application.Markets;
using RegimeDeck.Domain.Enums;

namespace RegimeDeck.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CandlesController : ControllerBase
{
    private readonly ICandleQueryService _candles;

    public CandlesController(ICandleQueryService candles) => _candles = candles;

    [HttpGet("{symbol}")]
    public async Task<IActionResult> Get(
        string symbol,
        [FromQuery] string interval = "OneDay",
        [FromQuery] int limit = 500,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<CandleInterval>(interval, true, out var parsedInterval))
            return BadRequest($"Invalid interval. Valid values: {string.Join(", ", Enum.GetNames<CandleInterval>())}");

        // Bound the page size: a negative limit becomes TOP(-1) -> SqlException on
        // SQL Server. 20000 gives headroom over the client's full-history request
        // (10000) — ~55 years of daily candles, more than any asset has.
        if (limit is < 1 or > 20000)
            return BadRequest("limit must be between 1 and 20000");

        var candles = await _candles.GetAsync(symbol, parsedInterval, limit, ct);

        if (candles.Count == 0)
            return NotFound($"No candles found for {symbol.ToUpperInvariant()} on {interval} interval");

        return Ok(candles);
    }
}
