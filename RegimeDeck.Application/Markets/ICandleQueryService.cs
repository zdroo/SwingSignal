using RegimeDeck.Contracts.Candles;
using RegimeDeck.Domain.Enums;

namespace RegimeDeck.Application.Markets;

public interface ICandleQueryService
{
    Task<List<CandleDto>> GetAsync(string symbol, CandleInterval interval, int limit, CancellationToken ct = default);
}
