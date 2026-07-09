using SwingSignal.Application.Abstractions.Persistence;
using SwingSignal.Contracts.Assets;
using SwingSignal.Domain.Entities;
using SwingSignal.Domain.Enums;

namespace SwingSignal.Application.Markets;

public class AssetCatalogService : IAssetCatalogService
{
    private readonly IAssetRepository _assets;

    public AssetCatalogService(IAssetRepository assets) => _assets = assets;

    public async Task<List<AssetDto>> GetAllActiveAsync(CancellationToken ct = default)
    {
        var assets = await _assets.GetAllActiveAsync(ct);
        return assets.Select(ToDto).ToList();
    }

    public async Task<AssetDto?> CreateAsync(
        string symbol, string name, MarketType marketType, CancellationToken ct = default)
    {
        var normalized = symbol.ToUpperInvariant();

        if (await _assets.ExistsAsync(normalized, ct))
            return null;

        var asset = new Asset
        {
            Symbol = normalized,
            Name = name,
            MarketType = marketType,
            IsActive = true
        };
        await _assets.AddAsync(asset, ct);

        return ToDto(asset);
    }

    private static AssetDto ToDto(Asset a) =>
        new(a.Id, a.Symbol, a.Name, a.MarketType.ToString(), a.IsActive);
}
