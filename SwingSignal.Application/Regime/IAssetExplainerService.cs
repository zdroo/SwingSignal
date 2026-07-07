using SwingSignal.Contracts.Regime;
using SwingSignal.Domain.Entities;
using SwingSignal.Domain.Enums;

namespace SwingSignal.Application.Regime;

public interface IAssetExplainerService
{
    Task<List<string>> GenerateAsync(
        string symbol,
        MarketType marketType,
        List<HistoricalMatchDto> matches,
        List<Candle>? candles = null,
        CancellationToken ct = default);
}
