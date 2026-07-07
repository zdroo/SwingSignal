using Microsoft.EntityFrameworkCore;
using SwingSignal.Application.Abstractions.Persistence;
using SwingSignal.Domain.Entities;

namespace SwingSignal.Infrastructure.Persistence.Repositories;

public class AssetRepository : IAssetRepository
{
    private readonly SwingSignalDbContext _context;

    public AssetRepository(SwingSignalDbContext context) => _context = context;

    public Task<List<Asset>> GetAllActiveAsync(CancellationToken ct = default) =>
        _context.Assets.Where(a => a.IsActive).OrderBy(a => a.Symbol).ToListAsync(ct);

    public Task<Asset?> GetBySymbolAsync(string symbol, CancellationToken ct = default) =>
        _context.Assets.FirstOrDefaultAsync(a => a.Symbol == symbol.ToUpper(), ct);

    public Task<bool> ExistsAsync(string symbol, CancellationToken ct = default) =>
        _context.Assets.AnyAsync(a => a.Symbol == symbol.ToUpper(), ct);

    public async Task AddAsync(Asset asset, CancellationToken ct = default)
    {
        _context.Assets.Add(asset);
        await _context.SaveChangesAsync(ct);
    }
}
