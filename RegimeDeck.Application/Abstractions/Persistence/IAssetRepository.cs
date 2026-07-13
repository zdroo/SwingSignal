using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Application.Abstractions.Persistence;

public interface IAssetRepository
{
    Task<List<Asset>> GetAllActiveAsync(CancellationToken ct = default);
    Task<Asset?> GetBySymbolAsync(string symbol, CancellationToken ct = default);
    Task<bool> ExistsAsync(string symbol, CancellationToken ct = default);
    Task AddAsync(Asset asset, CancellationToken ct = default);
}
