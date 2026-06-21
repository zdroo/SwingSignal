using SwingSignal.Domain.Entities;
using SwingSignal.Domain.Enums;

namespace SwingSignal.Application.Interfaces;

public interface ICandleRepository
{
    Task<List<Candle>> GetBySymbolAsync(string symbol, CandleInterval interval, int limit = 500);
    Task<DateTime?> GetLatestOpenTimeAsync(Guid assetId, CandleInterval interval);
    Task BulkInsertAsync(IEnumerable<Candle> candles);
}
