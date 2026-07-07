using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using SwingSignal.Application.Abstractions.Ingestion;
using SwingSignal.Application.Abstractions.Persistence;
using SwingSignal.Application.Markets;
using SwingSignal.Contracts.Assets;
using SwingSignal.Domain.Entities;
using SwingSignal.Domain.Enums;

namespace SwingSignal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AssetsController : ControllerBase
{
    private readonly IAssetRepository _assets;
    private readonly ISymbolSearchService _search;
    private readonly IPopularAssetsService _popular;
    private readonly IMemoryCache _cache;

    public AssetsController(
        IAssetRepository assets,
        ISymbolSearchService search,
        IPopularAssetsService popular,
        IMemoryCache cache)
    {
        _assets = assets;
        _search = search;
        _popular = popular;
        _cache = cache;
    }

    // Sparkline cards for the landing/dashboard strips. Public by design
    // (teaser tier) and cached — computing odds for 6 assets isn't free.
    [HttpGet("popular")]
    public async Task<IActionResult> Popular(CancellationToken ct)
    {
        var result = await _cache.GetOrCreateAsync("popular-assets", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return await _popular.GetPopularAsync(ct);
        });

        return Ok(result ?? []);
    }

    // Autocomplete for the asset search box — free text to symbol
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return Ok(new List<SymbolSearchResultDto>());

        var results = await _search.SearchAsync(q.Trim(), ct);
        return Ok(results);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var assets = await _assets.GetAllActiveAsync(ct);
        var dtos = assets.Select(a => new AssetDto(a.Id, a.Symbol, a.Name, a.MarketType.ToString(), a.IsActive));
        return Ok(dtos);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateAssetRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<MarketType>(request.MarketType, true, out var marketType))
            return BadRequest($"Invalid MarketType. Valid values: {string.Join(", ", Enum.GetNames<MarketType>())}");

        var symbol = request.Symbol.ToUpper();

        if (await _assets.ExistsAsync(symbol, ct))
            return Conflict($"Asset {symbol} already exists");

        var asset = new Asset { Symbol = symbol, Name = request.Name, MarketType = marketType, IsActive = true };
        await _assets.AddAsync(asset, ct);

        return CreatedAtAction(nameof(GetAll), new AssetDto(asset.Id, asset.Symbol, asset.Name, asset.MarketType.ToString(), asset.IsActive));
    }
}
