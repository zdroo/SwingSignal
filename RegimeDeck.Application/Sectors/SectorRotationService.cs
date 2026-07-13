using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Contracts.Sectors;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Application.Sectors;

/// Read side: serves the cached sector board ranked by regime edge (sectors
/// without a computable read sink last). Owns no compute.
public class SectorRotationService : ISectorRotationService
{
    private readonly ISectorRotationRepository _repository;

    public SectorRotationService(ISectorRotationRepository repository) => _repository = repository;

    public async Task<SectorRotationResultDto> GetAsync(CancellationToken ct = default)
    {
        var rows = await _repository.GetAllAsync(ct);

        var ranked = rows
            .OrderByDescending(r => r.Edge3M.HasValue)
            .ThenByDescending(r => r.Edge3M ?? double.MinValue)
            .ThenBy(r => r.Sector, StringComparer.Ordinal)
            .Select(Map)
            .ToList();

        return new SectorRotationResultDto(
            Sectors: ranked,
            Benchmark: SectorUniverse.Benchmark,
            AsOf: rows.Count > 0 ? rows.Max(r => r.ComputedAt) : null);
    }

    private static SectorRotationRowDto Map(SectorRotationRow r) =>
        new(r.Symbol, r.Sector, r.Odds3M, r.Edge3M, r.Stance, r.RelStrength3M);
}
