using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Contracts.Events;

namespace RegimeDeck.Application.Events;

/// Read side: serves the cached upcoming macro releases. Owns no ingestion.
public class EconomicCalendarService : IEconomicCalendarService
{
    private const int MaxTake = 20;

    private readonly IEconomicEventRepository _repository;

    public EconomicCalendarService(IEconomicEventRepository repository) => _repository = repository;

    public async Task<UpcomingEventsDto> GetUpcomingAsync(int take = 5, CancellationToken ct = default)
    {
        var capped = Math.Clamp(take, 1, MaxTake);
        var events = await _repository.GetUpcomingAsync(DateTime.UtcNow.Date, capped, ct);

        return new UpcomingEventsDto(
            events.Select(e => new EconomicEventDto(e.Title, e.Date, e.Impact)).ToList(),
            DateTime.UtcNow);
    }
}
