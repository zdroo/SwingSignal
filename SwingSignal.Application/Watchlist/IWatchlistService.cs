using SwingSignal.Contracts.Watchlist;

namespace SwingSignal.Application.Watchlist;

public interface IWatchlistService
{
    Task<List<WatchlistItemDto>> GetAsync(Guid userId, CancellationToken ct = default);
    Task<WatchlistItemDto> AddAsync(Guid userId, string symbol, CancellationToken ct = default);
    Task RemoveAsync(Guid userId, string symbol, CancellationToken ct = default);
    Task<List<WatchlistRowDto>> GetOverviewAsync(Guid userId, CancellationToken ct = default);
}
