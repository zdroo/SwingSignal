using Microsoft.AspNetCore.Mvc;
using RegimeDeck.Api.Extensions;
using RegimeDeck.Application.Alerts;
using RegimeDeck.Contracts.Alerts;

namespace RegimeDeck.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlertsController : ControllerBase
{
    private readonly IAlertRuleService _alerts;

    public AlertsController(IAlertRuleService alerts) => _alerts = alerts;

    [HttpGet("rules")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized("Create a free account to set up alerts.");
        return Ok(await _alerts.GetAsync(User.RequireUserId(), ct));
    }

    [HttpPost("rules")]
    public async Task<IActionResult> Create([FromBody] CreateAlertRuleRequest request, CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized("Create a free account to set up alerts.");
        return Ok(await _alerts.CreateAsync(User.RequireUserId(), request, ct));
    }

    [HttpPut("rules/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAlertRuleRequest request, CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized();
        return Ok(await _alerts.UpdateAsync(User.RequireUserId(), id, request, ct));
    }

    [HttpDelete("rules/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized();
        await _alerts.DeleteAsync(User.RequireUserId(), id, ct);
        return NoContent();
    }
}
