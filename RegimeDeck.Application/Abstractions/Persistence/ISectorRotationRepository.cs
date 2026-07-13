using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Application.Abstractions.Persistence;

public interface ISectorRotationRepository
{
    Task<List<SectorRotationRow>> GetAllAsync(CancellationToken ct = default);
    Task UpsertAsync(SectorRotationRow row, CancellationToken ct = default);
}
