using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Contracts.Macro;
using RegimeDeck.Domain.Enums;

namespace RegimeDeck.Application.MacroData;

public class MacroQueryService : IMacroQueryService
{
    private readonly IMacroRepository _macro;

    public MacroQueryService(IMacroRepository macro) => _macro = macro;

    public async Task<MacroSnapshotDto> GetLatestSnapshotAsync(CancellationToken ct = default)
    {
        var points = await _macro.GetLatestSnapshotAsync(ct);

        var indicators = points.ToDictionary(
            p => p.IndicatorType.ToString(),
            p => (decimal?)p.Value);

        var asOf = points.Count > 0 ? points.Max(p => p.Date) : DateTime.UtcNow;

        return new MacroSnapshotDto(indicators, asOf);
    }

    public async Task<List<MacroDataPointDto>> GetHistoryAsync(
        MacroIndicatorType type, int limit, CancellationToken ct = default)
    {
        var points = await _macro.GetByTypeAsync(type, limit, ct);

        return points
            .Select(p => new MacroDataPointDto(p.IndicatorType.ToString(), p.Date, p.Value, p.Source))
            .ToList();
    }
}
