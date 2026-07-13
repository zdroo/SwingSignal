using Microsoft.EntityFrameworkCore;
using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Infrastructure.Persistence.Repositories;

public class SectorRotationRepository : ISectorRotationRepository
{
    private readonly RegimeDeckDbContext _context;

    public SectorRotationRepository(RegimeDeckDbContext context) => _context = context;

    public Task<List<SectorRotationRow>> GetAllAsync(CancellationToken ct = default) =>
        _context.SectorRotationRows.AsNoTracking().ToListAsync(ct);

    public async Task UpsertAsync(SectorRotationRow row, CancellationToken ct = default)
    {
        var existing = await _context.SectorRotationRows.FirstOrDefaultAsync(r => r.Symbol == row.Symbol, ct);
        if (existing is null)
        {
            _context.SectorRotationRows.Add(row);
        }
        else
        {
            existing.Sector = row.Sector;
            existing.Odds3M = row.Odds3M;
            existing.Edge3M = row.Edge3M;
            existing.Stance = row.Stance;
            existing.RelStrength3M = row.RelStrength3M;
            existing.ComputedAt = row.ComputedAt;
        }
        await _context.SaveChangesAsync(ct);
    }
}
