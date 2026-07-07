using SwingSignal.Contracts.Assets;

namespace SwingSignal.Application.Markets;

public interface IPopularAssetsService
{
    Task<List<PopularAssetDto>> GetPopularAsync(CancellationToken ct = default);
}
