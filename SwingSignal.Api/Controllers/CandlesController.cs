using Microsoft.AspNetCore.Mvc;
using SwingSignal.Application.Markets;
using SwingSignal.Domain.Enums;

namespace SwingSignal.Api.Controllers;

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

        var candles = await _candles.GetAsync(symbol, parsedInterval, limit, ct);

        if (candles.Count == 0)
            return NotFound($"No candles found for {symbol.ToUpperInvariant()} on {interval} interval");

        return Ok(candles);
    }
}
