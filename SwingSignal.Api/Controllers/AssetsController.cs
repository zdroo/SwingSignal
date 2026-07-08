using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SwingSignal.Application.Abstractions.Ingestion;
using SwingSignal.Application.Markets;
using SwingSignal.Contracts.Assets;
using SwingSignal.Domain.Enums;

namespace SwingSignal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AssetsController : ControllerBase
{
    private readonly IAssetCatalogService _catalog;
    private readonly ISymbolSearchService _search;
    private readonly IPopularAssetsService _popular;

    public AssetsController(
        IAssetCatalogService catalog,
        ISymbolSearchService search,
        IPopularAssetsService popular)
    {
        _catalog = catalog;
        _search = search;
        _popular = popular;
    }

    // Sparkline cards for the landing/dashboard strips. Public by design (teaser tier).
    [HttpGet("popular")]
    public async Task<IActionResult> Popular(CancellationToken ct) =>
        Ok(await _popular.GetPopularAsync(ct));

    // Rate limited: it proxies Yahoo, and abuse could get our IP banned there.
    [HttpGet("search")]
    [EnableRateLimiting("public-sensitive")]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return Ok(new List<SymbolSearchResultDto>());

        var results = await _search.SearchAsync(q.Trim(), ct);
        return Ok(results);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        Ok(await _catalog.GetAllActiveAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateAssetRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<MarketType>(request.MarketType, true, out var marketType))
            return BadRequest($"Invalid MarketType. Valid values: {string.Join(", ", Enum.GetNames<MarketType>())}");

        var created = await _catalog.CreateAsync(request.Symbol, request.Name, marketType, ct);

        return created is null
            ? Conflict($"Asset {request.Symbol.ToUpper()} already exists")
            : CreatedAtAction(nameof(GetAll), created);
    }
}
