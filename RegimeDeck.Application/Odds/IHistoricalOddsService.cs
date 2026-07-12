using RegimeDeck.Contracts.Regime;

namespace RegimeDeck.Application.Odds;

public interface IHistoricalOddsService
{
    Task<AssetOddsDto> GetOddsAsync(string symbol, CancellationToken ct = default);

    Task<AssetPeriodOddsDto> GetOddsForDaysAsync(string symbol, int days, CancellationToken ct = default);
}
