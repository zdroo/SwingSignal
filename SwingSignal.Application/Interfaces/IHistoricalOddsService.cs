using SwingSignal.Application.DTOs;

namespace SwingSignal.Application.Interfaces;

public interface IHistoricalOddsService
{
    Task<AssetOddsDto> GetOddsAsync(string symbol, int topK = 10, CancellationToken ct = default);
}
