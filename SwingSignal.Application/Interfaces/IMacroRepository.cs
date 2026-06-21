using SwingSignal.Domain.Entities;
using SwingSignal.Domain.Enums;

namespace SwingSignal.Application.Interfaces;

public interface IMacroRepository
{
    Task<List<MacroDataPoint>> GetByTypeAsync(MacroIndicatorType type, int limit = 100);
    Task<MacroDataPoint?> GetLatestAsync(MacroIndicatorType type);
    Task<DateTime?> GetLatestDateAsync(MacroIndicatorType type);
    Task BulkInsertAsync(IEnumerable<MacroDataPoint> points);
    Task<List<MacroDataPoint>> GetLatestSnapshotAsync();
}
