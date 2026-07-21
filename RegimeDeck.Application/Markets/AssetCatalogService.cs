using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Contracts.Assets;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Application.Markets;

public class AssetCatalogService : IAssetCatalogService
{
    private readonly IAssetRepository _assets;

    public AssetCatalogService(IAssetRepository assets) => _assets = assets;

    public async Task<List<AssetDto>> GetAllActiveAsync(CancellationToken ct = default)
    {
        var assets = await _assets.GetAllActiveAsync(ct);
        return assets.Select(ToDto).ToList();
    }

    private static AssetDto ToDto(Asset a) =>
        new(a.Id, a.Symbol, a.Name, a.MarketType.ToString(), a.IsActive);
}
