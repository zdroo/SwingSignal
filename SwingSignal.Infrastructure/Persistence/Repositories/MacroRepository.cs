using Microsoft.EntityFrameworkCore;
using SwingSignal.Application.Abstractions.Persistence;
using SwingSignal.Domain.Entities;
using SwingSignal.Domain.Enums;

namespace SwingSignal.Infrastructure.Persistence.Repositories;

public class MacroRepository : IMacroRepository
{
    private readonly SwingSignalDbContext _context;

    public MacroRepository(SwingSignalDbContext context) => _context = context;

    public async Task<List<MacroDataPoint>> GetByTypeAsync(
        MacroIndicatorType type, int limit = 100, CancellationToken ct = default) =>
        await _context.MacroDataPoints.AsNoTracking()
            .Where(m => m.IndicatorType == type)
            .OrderByDescending(m => m.Date)
            .Take(limit)
            .OrderBy(m => m.Date)
            .ToListAsync(ct);

    public Task<List<MacroDataPoint>> GetForTypesAsync(
        IReadOnlyCollection<MacroIndicatorType> types, CancellationToken ct = default) =>
        _context.MacroDataPoints.AsNoTracking()
            .Where(m => types.Contains(m.IndicatorType))
            .OrderBy(m => m.Date)
            .ToListAsync(ct);

    public Task<List<MacroDataPoint>> GetSinceAsync(
        MacroIndicatorType type, DateTime from, CancellationToken ct = default) =>
        _context.MacroDataPoints.AsNoTracking()
            .Where(m => m.IndicatorType == type && m.Date >= from)
            .OrderBy(m => m.Date)
            .ToListAsync(ct);

    public Task<MacroDataPoint?> GetLatestAsync(MacroIndicatorType type, CancellationToken ct = default) =>
        _context.MacroDataPoints.AsNoTracking()
            .Where(m => m.IndicatorType == type)
            .OrderByDescending(m => m.Date)
            .FirstOrDefaultAsync(ct);

    public Task<DateTime?> GetLatestDateAsync(MacroIndicatorType type, CancellationToken ct = default) =>
        _context.MacroDataPoints.AsNoTracking()
            .Where(m => m.IndicatorType == type)
            .MaxAsync(m => (DateTime?)m.Date, ct);

    public Task<DateTime?> GetLatestDateOverallAsync(CancellationToken ct = default) =>
        _context.MacroDataPoints.MaxAsync(m => (DateTime?)m.Date, ct);

    public async Task BulkInsertAsync(IEnumerable<MacroDataPoint> points, CancellationToken ct = default)
    {
        _context.MacroDataPoints.AddRange(points);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<MacroDataPoint>> GetLatestSnapshotAsync(CancellationToken ct = default)
    {
        var types = Enum.GetValues<MacroIndicatorType>();
        var result = new List<MacroDataPoint>();

        foreach (var type in types)
        {
            var latest = await GetLatestAsync(type, ct);
            if (latest is not null)
                result.Add(latest);
        }

        return result;
    }
}
