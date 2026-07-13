using Microsoft.EntityFrameworkCore;
using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Infrastructure.Persistence.Repositories;

public class ScreenerRepository : IScreenerRepository
{
    private readonly RegimeDeckDbContext _context;

    public ScreenerRepository(RegimeDeckDbContext context) => _context = context;

    public Task<List<ScreenerRow>> GetAllAsync(CancellationToken ct = default) =>
        _context.ScreenerRows.AsNoTracking().ToListAsync(ct);

    // Upsert by symbol: each compute run overwrites the row in place, so the
    // table holds exactly one current row per universe symbol.
    public async Task UpsertAsync(ScreenerRow row, CancellationToken ct = default)
    {
        var existing = await _context.ScreenerRows.FirstOrDefaultAsync(r => r.Symbol == row.Symbol, ct);
        if (existing is null)
        {
            _context.ScreenerRows.Add(row);
        }
        else
        {
            existing.Name = row.Name;
            existing.MarketType = row.MarketType;
            existing.CurrentPrice = row.CurrentPrice;
            existing.Odds3M = row.Odds3M;
            existing.BaseRate3M = row.BaseRate3M;
            existing.Edge3M = row.Edge3M;
            existing.Stance = row.Stance;
            existing.Strength = row.Strength;
            existing.ComputedAt = row.ComputedAt;
        }
        await _context.SaveChangesAsync(ct);
    }
}
