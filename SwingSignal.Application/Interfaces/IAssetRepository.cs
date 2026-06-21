using SwingSignal.Domain.Entities;

namespace SwingSignal.Application.Interfaces;

public interface IAssetRepository
{
    Task<List<Asset>> GetAllActiveAsync();
    Task<Asset?> GetBySymbolAsync(string symbol);
    Task<bool> ExistsAsync(string symbol);
    Task AddAsync(Asset asset);
}
