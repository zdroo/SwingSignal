using Microsoft.EntityFrameworkCore;
using SwingSignal.Application.Abstractions.Persistence;
using SwingSignal.Domain.Entities;

namespace SwingSignal.Infrastructure.Persistence.Repositories;

public class AnalyticsRepository : IAnalyticsRepository
{
    private readonly SwingSignalDbContext _context;

    public AnalyticsRepository(SwingSignalDbContext context) => _context = context;

    public async Task LogSearchAsync(SearchLog entry, CancellationToken ct = default)
    {
        _context.SearchLogs.Add(entry);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<(string Symbol, int Views)>> GetTopSymbolsAsync(int days, int count, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-days);

        var rows = await _context.SearchLogs
            .Where(s => s.CreatedAt >= cutoff)
            .GroupBy(s => s.Symbol)
            .Select(g => new { Symbol = g.Key, Views = g.Count() })
            .OrderByDescending(g => g.Views)
            .Take(count)
            .ToListAsync(ct);

        return rows.Select(r => (r.Symbol, r.Views)).ToList();
    }

    public Task DetachUserAsync(Guid userId, CancellationToken ct = default) =>
        _context.SearchLogs
            .Where(s => s.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UserId, (Guid?)null), ct);

    public async Task<bool> AddToWaitlistAsync(WaitlistEntry entry, CancellationToken ct = default)
    {
        var exists = await _context.WaitlistEntries.AnyAsync(w => w.Email == entry.Email, ct);
        if (exists) return false;

        _context.WaitlistEntries.Add(entry);

        try
        {
            await _context.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Unique index race — someone else inserted the same email concurrently
            return false;
        }
    }
}
