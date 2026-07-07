using SwingSignal.Contracts.Regime;

namespace SwingSignal.Application.Odds;

public interface IHistoricalOddsService
{
    Task<AssetOddsDto> GetOddsAsync(string symbol, int topK = 10, CancellationToken ct = default);

    Task<AssetPeriodOddsDto> GetOddsForDaysAsync(string symbol, int days, int topK = 10, CancellationToken ct = default);
}
