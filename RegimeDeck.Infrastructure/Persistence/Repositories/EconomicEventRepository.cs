using Microsoft.EntityFrameworkCore;
using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Infrastructure.Persistence.Repositories;

public class EconomicEventRepository : IEconomicEventRepository
{
    private readonly RegimeDeckDbContext _context;

    public EconomicEventRepository(RegimeDeckDbContext context) => _context = context;

    public Task<List<EconomicEvent>> GetUpcomingAsync(DateTime from, int take, CancellationToken ct = default) =>
        _context.EconomicEvents
            .AsNoTracking()
            .Where(e => e.Date >= from)
            .OrderBy(e => e.Date)
            .Take(take)
            .ToListAsync(ct);

    public async Task UpsertAsync(EconomicEvent evt, CancellationToken ct = default)
    {
        var existing = await _context.EconomicEvents
            .FirstOrDefaultAsync(e => e.ReleaseId == evt.ReleaseId && e.Date == evt.Date, ct);
        if (existing is null)
        {
            _context.EconomicEvents.Add(evt);
        }
        else
        {
            existing.Title = evt.Title;
            existing.Impact = evt.Impact;
        }
        await _context.SaveChangesAsync(ct);
    }

    public Task<int> DeleteBeforeAsync(DateTime cutoff, CancellationToken ct = default) =>
        _context.EconomicEvents.Where(e => e.Date < cutoff).ExecuteDeleteAsync(ct);
}
