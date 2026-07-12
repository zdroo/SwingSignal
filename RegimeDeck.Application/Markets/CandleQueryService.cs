using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Contracts.Candles;
using RegimeDeck.Domain.Enums;

namespace RegimeDeck.Application.Markets;

public class CandleQueryService : ICandleQueryService
{
    private readonly ICandleRepository _candles;

    public CandleQueryService(ICandleRepository candles) => _candles = candles;

    public async Task<List<CandleDto>> GetAsync(
        string symbol, CandleInterval interval, int limit, CancellationToken ct = default)
    {
        var candles = await _candles.GetBySymbolAsync(symbol, interval, limit, ct);

        return candles
            .Select(c => new CandleDto(c.OpenTime, c.Open, c.High, c.Low, c.Close, c.Volume))
            .ToList();
    }
}
