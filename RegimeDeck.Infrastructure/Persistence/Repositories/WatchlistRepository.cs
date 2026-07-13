using Microsoft.EntityFrameworkCore;
using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Application.Common;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Infrastructure.Persistence.Repositories;

public class WatchlistRepository : IWatchlistRepository
{
    private readonly RegimeDeckDbContext _context;

    public WatchlistRepository(RegimeDeckDbContext context) => _context = context;

    public Task<List<WatchlistItem>> GetByUserAsync(Guid userId, CancellationToken ct = default) =>
        _context.WatchlistItems
            .AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderBy(w => w.CreatedAt)
            .ToListAsync(ct);

    public Task<WatchlistItem?> GetAsync(Guid userId, string symbol, CancellationToken ct = default) =>
        _context.WatchlistItems
            .FirstOrDefaultAsync(w => w.UserId == userId && w.Symbol == symbol, ct);

    public Task<int> CountByUserAsync(Guid userId, CancellationToken ct = default) =>
        _context.WatchlistItems.CountAsync(w => w.UserId == userId, ct);

    public async Task AddAsync(WatchlistItem item, CancellationToken ct = default)
    {
        _context.WatchlistItems.Add(item);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two concurrent adds raced past the service's duplicate check —
            // the unique (UserId, Symbol) index is the real referee
            throw new ConflictException($"{item.Symbol} is already on your watchlist.");
        }
    }

    public async Task DeleteAsync(WatchlistItem item, CancellationToken ct = default)
    {
        _context.WatchlistItems.Remove(item);
        await _context.SaveChangesAsync(ct);
    }
}
