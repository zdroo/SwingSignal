namespace RegimeDeck.Contracts.Events;

public record EconomicEventDto(string Title, DateTime Date, string Impact);

public record UpcomingEventsDto(List<EconomicEventDto> Events, DateTime AsOf);
