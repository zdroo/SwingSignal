using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Application.Abstractions.Persistence;

public interface IEconomicEventRepository
{
    /// Events on or after `from`, soonest first, capped at `take`.
    Task<List<EconomicEvent>> GetUpcomingAsync(DateTime from, int take, CancellationToken ct = default);
    Task UpsertAsync(EconomicEvent evt, CancellationToken ct = default);
    /// Housekeeping: drop occurrences that are already in the past.
    Task<int> DeleteBeforeAsync(DateTime cutoff, CancellationToken ct = default);
}
