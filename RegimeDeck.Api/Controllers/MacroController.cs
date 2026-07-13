using Microsoft.AspNetCore.Mvc;
using RegimeDeck.Application.MacroData;
using RegimeDeck.Domain.Enums;

namespace RegimeDeck.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MacroController : ControllerBase
{
    private readonly IMacroQueryService _macro;

    public MacroController(IMacroQueryService macro) => _macro = macro;

    [HttpGet("snapshot")]
    public async Task<IActionResult> GetSnapshot(CancellationToken ct) =>
        Ok(await _macro.GetLatestSnapshotAsync(ct));

    [HttpGet("{indicatorType}")]
    public async Task<IActionResult> GetHistory(
        string indicatorType, [FromQuery] int limit = 100, CancellationToken ct = default)
    {
        if (!Enum.TryParse<MacroIndicatorType>(indicatorType, true, out var parsedType))
            return BadRequest($"Invalid indicator. Valid values: {string.Join(", ", Enum.GetNames<MacroIndicatorType>())}");

        var points = await _macro.GetHistoryAsync(parsedType, limit, ct);

        if (points.Count == 0)
            return NotFound($"No data found for {indicatorType}");

        return Ok(points);
    }
}
