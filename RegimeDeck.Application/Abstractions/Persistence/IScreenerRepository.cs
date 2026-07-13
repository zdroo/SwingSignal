using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Application.Abstractions.Persistence;

public interface IScreenerRepository
{
    Task<List<ScreenerRow>> GetAllAsync(CancellationToken ct = default);
    Task UpsertAsync(ScreenerRow row, CancellationToken ct = default);
}
