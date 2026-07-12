using RegimeDeck.Contracts.Regime;
using RegimeDeck.Domain.Entities;
using RegimeDeck.Domain.Enums;

namespace RegimeDeck.Application.Regime;

public interface IAssetExplainerService
{
    Task<List<string>> GenerateAsync(
        string symbol,
        MarketType marketType,
        List<HistoricalMatchDto> matches,
        List<Candle>? candles = null,
        CancellationToken ct = default);
}
