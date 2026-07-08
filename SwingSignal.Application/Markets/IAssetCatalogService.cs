using SwingSignal.Contracts.Assets;
using SwingSignal.Domain.Enums;

namespace SwingSignal.Application.Markets;

public interface IAssetCatalogService
{
    Task<List<AssetDto>> GetAllActiveAsync(CancellationToken ct = default);

    /// Returns null when the symbol is already registered.
    Task<AssetDto?> CreateAsync(string symbol, string name, MarketType marketType, CancellationToken ct = default);
}
