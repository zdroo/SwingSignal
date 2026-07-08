using SwingSignal.Application.Abstractions.Persistence;
using SwingSignal.Contracts.Candles;
using SwingSignal.Domain.Enums;

namespace SwingSignal.Application.Markets;

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
