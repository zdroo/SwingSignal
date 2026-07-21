using RegimeDeck.Contracts.Assets;

namespace RegimeDeck.Application.Markets;

public interface IAssetCatalogService
{
    Task<List<AssetDto>> GetAllActiveAsync(CancellationToken ct = default);
}
