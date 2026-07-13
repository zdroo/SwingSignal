using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Contracts.Screener;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Application.Screener;

/// Read side: serves the cached board, delegating the free/Pro trim, filters
/// and ranking to ScreenerProjection. Owns no compute — that's the background
/// service's job.
public class ScreenerService : IScreenerService
{
    private readonly IScreenerRepository _repository;

    public ScreenerService(IScreenerRepository repository) => _repository = repository;

    public async Task<ScreenerResultDto> GetAsync(bool isPro, ScreenerQuery? query, CancellationToken ct = default)
    {
        var all = await _repository.GetAllAsync(ct);
        var rows = ScreenerProjection.Project(all, isPro, query);

        return new ScreenerResultDto(
            Rows: rows.Select(Map).ToList(),
            AsOf: all.Count > 0 ? all.Max(r => r.ComputedAt) : null,
            UniverseSize: all.Count,
            Trimmed: !isPro);
    }

    private static ScreenerRowDto Map(ScreenerRow r) =>
        new(r.Symbol, r.Name, r.MarketType.ToString(), r.CurrentPrice,
            r.Odds3M, r.BaseRate3M, r.Edge3M, r.Stance, r.Strength);
}
