using Microsoft.AspNetCore.Mvc;
using RegimeDeck.Application.Events;

namespace RegimeDeck.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly IEconomicCalendarService _calendar;

    public EventsController(IEconomicCalendarService calendar) => _calendar = calendar;

    // Upcoming high-impact macro releases — global market info, free for all,
    // a cheap cache read.
    [HttpGet("upcoming")]
    public async Task<IActionResult> Upcoming([FromQuery] int take = 5, CancellationToken ct = default) =>
        Ok(await _calendar.GetUpcomingAsync(take, ct));
}
