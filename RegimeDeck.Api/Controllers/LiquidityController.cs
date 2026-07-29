using Microsoft.AspNetCore.Mvc;
using RegimeDeck.Application.Liquidity;

namespace RegimeDeck.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LiquidityController : ControllerBase
{
    private readonly ILiquidityService _liquidity;

    public LiquidityController(ILiquidityService liquidity) => _liquidity = liquidity;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(await _liquidity.GetDashboardAsync(ct));
}
