using Microsoft.EntityFrameworkCore;
using SwingSignal.Application.Interfaces;
using SwingSignal.Domain.Entities;

namespace SwingSignal.Infrastructure.Repositories;

public class AssetRepository : IAssetRepository
{
    private readonly SwingSignalDbContext _context;

    public AssetRepository(SwingSignalDbContext context) => _context = context;

    public async Task<List<Asset>> GetAllActiveAsync() =>
        await _context.Assets.Where(a => a.IsActive).OrderBy(a => a.Symbol).ToListAsync();

    public async Task<Asset?> GetBySymbolAsync(string symbol) =>
        await _context.Assets.FirstOrDefaultAsync(a => a.Symbol == symbol.ToUpper());

    public async Task<bool> ExistsAsync(string symbol) =>
        await _context.Assets.AnyAsync(a => a.Symbol == symbol.ToUpper());

    public async Task AddAsync(Asset asset) =>
        await _context.Assets.AddAsync(asset);
}
