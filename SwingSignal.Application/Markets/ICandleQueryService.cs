using SwingSignal.Contracts.Candles;
using SwingSignal.Domain.Enums;

namespace SwingSignal.Application.Markets;

public interface ICandleQueryService
{
    Task<List<CandleDto>> GetAsync(string symbol, CandleInterval interval, int limit, CancellationToken ct = default);
}
