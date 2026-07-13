using Microsoft.AspNetCore.Mvc;
using RegimeDeck.Application.Sectors;

namespace RegimeDeck.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SectorsController : ControllerBase
{
    private readonly ISectorRotationService _sectors;

    public SectorsController(ISectorRotationService sectors) => _sectors = sectors;

    // The current sector board — free for everyone (a cheap cache read, like
    // the regime dashboard). Pro history/alerts come in a later phase.
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(await _sectors.GetAsync(ct));
}
