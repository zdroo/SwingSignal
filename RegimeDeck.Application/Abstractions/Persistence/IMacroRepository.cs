using RegimeDeck.Domain.Entities;
using RegimeDeck.Domain.Enums;

namespace RegimeDeck.Application.Abstractions.Persistence;

public interface IMacroRepository
{
    Task<List<MacroDataPoint>> GetByTypeAsync(MacroIndicatorType type, int limit = 100, CancellationToken ct = default);

    /// All points for the given indicators, ordered by date ascending.
    Task<List<MacroDataPoint>> GetForTypesAsync(IReadOnlyCollection<MacroIndicatorType> types, CancellationToken ct = default);

    /// Points for one indicator from a given date onward, ordered by date ascending.
    Task<List<MacroDataPoint>> GetSinceAsync(MacroIndicatorType type, DateTime from, CancellationToken ct = default);

    Task<MacroDataPoint?> GetLatestAsync(MacroIndicatorType type, CancellationToken ct = default);
    Task<DateTime?> GetLatestDateAsync(MacroIndicatorType type, CancellationToken ct = default);
    Task<DateTime?> GetLatestDateOverallAsync(CancellationToken ct = default);
    Task BulkInsertAsync(IEnumerable<MacroDataPoint> points, CancellationToken ct = default);
    Task<List<MacroDataPoint>> GetLatestSnapshotAsync(CancellationToken ct = default);
}
