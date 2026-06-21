using Microsoft.EntityFrameworkCore;
using SwingSignal.Application.Interfaces;
using SwingSignal.Domain.Entities;
using SwingSignal.Domain.Enums;

namespace SwingSignal.Infrastructure.Repositories;

public class CandleRepository : ICandleRepository
{
    private readonly SwingSignalDbContext _context;

    public CandleRepository(SwingSignalDbContext context) => _context = context;

    public async Task<List<Candle>> GetBySymbolAsync(string symbol, CandleInterval interval, int limit = 500) =>
        await _context.Candles
            .Include(c => c.Asset)
            .Where(c => c.Asset.Symbol == symbol.ToUpper() && c.Interval == interval)
            .OrderByDescending(c => c.OpenTime)
            .Take(limit)
            .OrderBy(c => c.OpenTime)
            .ToListAsync();

    public async Task<DateTime?> GetLatestOpenTimeAsync(Guid assetId, CandleInterval interval) =>
        await _context.Candles
            .Where(c => c.AssetId == assetId && c.Interval == interval)
            .MaxAsync(c => (DateTime?)c.OpenTime);

    public async Task BulkInsertAsync(IEnumerable<Candle> candles)
    {
        await _context.Candles.AddRangeAsync(candles);
    }
}
