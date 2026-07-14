using RegimeDeck.Contracts.Events;

namespace RegimeDeck.Application.Events;

public interface IEconomicCalendarService
{
    /// The next `take` high-impact macro releases from today onward.
    Task<UpcomingEventsDto> GetUpcomingAsync(int take = 5, CancellationToken ct = default);
}
