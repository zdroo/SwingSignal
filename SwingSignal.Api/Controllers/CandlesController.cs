using Microsoft.AspNetCore.Mvc;
using SwingSignal.Application.DTOs;
using SwingSignal.Application.Interfaces;
using SwingSignal.Domain.Enums;

namespace SwingSignal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CandlesController : ControllerBase
{
    private readonly ICandleRepository _candles;

    public CandlesController(ICandleRepository candles) => _candles = candles;

    [HttpGet("{symbol}")]
    public async Task<IActionResult> Get(
        string symbol,
        [FromQuery] string interval = "OneDay",
        [FromQuery] int limit = 500)
    {
        if (!Enum.TryParse<CandleInterval>(interval, true, out var parsedInterval))
            return BadRequest($"Invalid interval. Valid values: {string.Join(", ", Enum.GetNames<CandleInterval>())}");

        var candles = await _candles.GetBySymbolAsync(symbol, parsedInterval, limit);

        if (candles.Count == 0)
            return NotFound($"No candles found for {symbol.ToUpper()} on {interval} interval");

        var dtos = candles.Select(c => new CandleDto(c.OpenTime, c.Open, c.High, c.Low, c.Close, c.Volume));
        return Ok(dtos);
    }
}
