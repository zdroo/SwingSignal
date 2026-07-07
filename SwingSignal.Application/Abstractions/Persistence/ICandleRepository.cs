using SwingSignal.Domain.Entities;
using SwingSignal.Domain.Enums;

namespace SwingSignal.Application.Abstractions.Persistence;

public interface ICandleRepository
{
    Task<List<Candle>> GetBySymbolAsync(string symbol, CandleInterval interval, int limit = 500, CancellationToken ct = default);

    /// Full daily candle history for an asset, ordered by open time ascending.
    Task<List<Candle>> GetDailyHistoryAsync(Guid assetId, CancellationToken ct = default);

    Task<DateTime?> GetLatestOpenTimeAsync(Guid assetId, CandleInterval interval, CancellationToken ct = default);
    Task BulkInsertAsync(IEnumerable<Candle> candles, CancellationToken ct = default);
}
