using Microsoft.AspNetCore.Mvc;
using SwingSignal.Application.Abstractions.Persistence;
using SwingSignal.Contracts.Macro;
using SwingSignal.Domain.Enums;

namespace SwingSignal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MacroController : ControllerBase
{
    private readonly IMacroRepository _macro;

    public MacroController(IMacroRepository macro) => _macro = macro;

    [HttpGet("snapshot")]
    public async Task<IActionResult> GetSnapshot(CancellationToken ct)
    {
        var points = await _macro.GetLatestSnapshotAsync(ct);

        var indicators = points.ToDictionary(
            p => p.IndicatorType.ToString(),
            p => (decimal?)p.Value);

        var asOf = points.Count > 0 ? points.Max(p => p.Date) : DateTime.UtcNow;

        return Ok(new MacroSnapshotDto(indicators, asOf));
    }

    [HttpGet("{indicatorType}")]
    public async Task<IActionResult> GetHistory(
        string indicatorType, [FromQuery] int limit = 100, CancellationToken ct = default)
    {
        if (!Enum.TryParse<MacroIndicatorType>(indicatorType, true, out var parsedType))
            return BadRequest($"Invalid indicator. Valid values: {string.Join(", ", Enum.GetNames<MacroIndicatorType>())}");

        var points = await _macro.GetByTypeAsync(parsedType, limit, ct);

        if (points.Count == 0)
            return NotFound($"No data found for {indicatorType}");

        var dtos = points.Select(p => new MacroDataPointDto(p.IndicatorType.ToString(), p.Date, p.Value, p.Source));
        return Ok(dtos);
    }
}
