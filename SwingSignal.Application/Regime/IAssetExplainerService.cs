using SwingSignal.Contracts.Regime;
using SwingSignal.Domain.Enums;

namespace SwingSignal.Application.Regime;

public interface IAssetExplainerService
{
    Task<List<string>> GenerateAsync(
        string symbol,
        MarketType marketType,
        List<HistoricalMatchDto> matches,
        CancellationToken ct = default);
}
