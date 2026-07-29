using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RegimeDeck.Application.Portfolio;
using RegimeDeck.Contracts.Portfolio;

namespace RegimeDeck.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PortfolioController : ControllerBase
{
    private readonly IPortfolioXrayService _xray;

    public PortfolioController(IPortfolioXrayService xray) => _xray = xray;

    // Can trigger on-demand ingestion for unknown symbols — compute-budgeted.
    [HttpPost("xray")]
    [EnableRateLimiting("compute")]
    public async Task<IActionResult> Xray([FromBody] PortfolioXrayRequest request, CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized("Create a free account to X-ray your portfolio.");

        return Ok(await _xray.GetXrayAsync(request, ct));
    }
}
