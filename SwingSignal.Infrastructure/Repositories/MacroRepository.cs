using Microsoft.EntityFrameworkCore;
using SwingSignal.Application.Interfaces;
using SwingSignal.Domain.Entities;
using SwingSignal.Domain.Enums;

namespace SwingSignal.Infrastructure.Repositories;

public class MacroRepository : IMacroRepository
{
    private readonly SwingSignalDbContext _context;

    public MacroRepository(SwingSignalDbContext context) => _context = context;

    public async Task<List<MacroDataPoint>> GetByTypeAsync(MacroIndicatorType type, int limit = 100) =>
        await _context.MacroDataPoints
            .Where(m => m.IndicatorType == type)
            .OrderByDescending(m => m.Date)
            .Take(limit)
            .OrderBy(m => m.Date)
            .ToListAsync();

    public async Task<MacroDataPoint?> GetLatestAsync(MacroIndicatorType type) =>
        await _context.MacroDataPoints
            .Where(m => m.IndicatorType == type)
            .OrderByDescending(m => m.Date)
            .FirstOrDefaultAsync();

    public async Task<DateTime?> GetLatestDateAsync(MacroIndicatorType type) =>
        await _context.MacroDataPoints
            .Where(m => m.IndicatorType == type)
            .MaxAsync(m => (DateTime?)m.Date);

    public async Task BulkInsertAsync(IEnumerable<MacroDataPoint> points) =>
        await _context.MacroDataPoints.AddRangeAsync(points);

    public async Task<List<MacroDataPoint>> GetLatestSnapshotAsync()
    {
        var types = Enum.GetValues<MacroIndicatorType>();
        var result = new List<MacroDataPoint>();

        foreach (var type in types)
        {
            var latest = await GetLatestAsync(type);
            if (latest is not null)
                result.Add(latest);
        }

        return result;
    }
}
