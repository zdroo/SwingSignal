using SwingSignal.Contracts.Regime;

namespace SwingSignal.Application.Odds;

public interface IHistoricalOddsService
{
    Task<AssetOddsDto> GetOddsAsync(string symbol, CancellationToken ct = default);

    Task<AssetPeriodOddsDto> GetOddsForDaysAsync(string symbol, int days, CancellationToken ct = default);
}
