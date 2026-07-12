using RegimeDeck.Contracts.Assets;

namespace RegimeDeck.Application.Markets;

public interface IPopularAssetsService
{
    Task<List<PopularAssetDto>> GetPopularAsync(CancellationToken ct = default);
}
