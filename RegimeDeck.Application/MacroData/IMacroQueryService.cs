using RegimeDeck.Contracts.Macro;
using RegimeDeck.Domain.Enums;

namespace RegimeDeck.Application.MacroData;

/// Raw macro series browsing (latest snapshot, per-indicator history) —
/// distinct from IMacroRegimeService, which interprets the data.
public interface IMacroQueryService
{
    Task<MacroSnapshotDto> GetLatestSnapshotAsync(CancellationToken ct = default);
    Task<List<MacroDataPointDto>> GetHistoryAsync(MacroIndicatorType type, int limit, CancellationToken ct = default);
}
