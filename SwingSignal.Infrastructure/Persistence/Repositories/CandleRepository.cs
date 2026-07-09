using Microsoft.EntityFrameworkCore;
using SwingSignal.Application.Abstractions.Persistence;
using SwingSignal.Domain.Entities;
using SwingSignal.Domain.Enums;

namespace SwingSignal.Infrastructure.Persistence.Repositories;

public class CandleRepository : ICandleRepository
{
    private readonly SwingSignalDbContext _context;

    public CandleRepository(SwingSignalDbContext context) => _context = context;

    public async Task<List<Candle>> GetBySymbolAsync(
        string symbol, CandleInterval interval, int limit = 500, CancellationToken ct = default)
    {
        var upper = symbol.ToUpperInvariant();
        return await _context.Candles.AsNoTracking()
            .Include(c => c.Asset)
            .Where(c => c.Asset.Symbol == upper && c.Interval == interval)
            .OrderByDescending(c => c.OpenTime)
            .Take(limit)
            .OrderBy(c => c.OpenTime)
            .ToListAsync(ct);
    }

    public Task<List<Candle>> GetDailyHistoryAsync(Guid assetId, CancellationToken ct = default) =>
        _context.Candles.AsNoTracking()
            .Where(c => c.AssetId == assetId && c.Interval == CandleInterval.OneDay)
            .OrderBy(c => c.OpenTime)
            .ToListAsync(ct);

    public Task<DateTime?> GetLatestOpenTimeAsync(Guid assetId, CandleInterval interval, CancellationToken ct = default) =>
        _context.Candles.AsNoTracking()
            .Where(c => c.AssetId == assetId && c.Interval == interval)
            .MaxAsync(c => (DateTime?)c.OpenTime, ct);

    public async Task BulkInsertAsync(IEnumerable<Candle> candles, CancellationToken ct = default)
    {
        _context.Candles.AddRange(candles);
        await _context.SaveChangesAsync(ct);
    }
}
