using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Application.Abstractions.Persistence;

public interface IWatchlistRepository
{
    Task<List<WatchlistItem>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<WatchlistItem?> GetAsync(Guid userId, string symbol, CancellationToken ct = default);
    Task<int> CountByUserAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(WatchlistItem item, CancellationToken ct = default);
    Task DeleteAsync(WatchlistItem item, CancellationToken ct = default);
}
