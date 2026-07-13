using RegimeDeck.Contracts.Sectors;

namespace RegimeDeck.Application.Sectors;

public interface ISectorRotationService
{
    /// The sector board, ranked by regime edge. Free for everyone — the current
    /// snapshot; history/alerts are the Pro layer (added in a later phase).
    Task<SectorRotationResultDto> GetAsync(CancellationToken ct = default);
}
