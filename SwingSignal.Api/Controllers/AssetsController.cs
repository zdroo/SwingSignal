using Microsoft.AspNetCore.Mvc;
using SwingSignal.Application.DTOs;
using SwingSignal.Application.Interfaces;
using SwingSignal.Domain.Entities;
using SwingSignal.Domain.Enums;
using SwingSignal.Infrastructure;

namespace SwingSignal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AssetsController : ControllerBase
{
    private readonly IAssetRepository _assets;
    private readonly SwingSignalDbContext _db;

    public AssetsController(IAssetRepository assets, SwingSignalDbContext db)
    {
        _assets = assets;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var assets = await _assets.GetAllActiveAsync();
        var dtos = assets.Select(a => new AssetDto(a.Id, a.Symbol, a.Name, a.MarketType.ToString(), a.IsActive));
        return Ok(dtos);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateAssetRequest request)
    {
        if (!Enum.TryParse<MarketType>(request.MarketType, true, out var marketType))
            return BadRequest($"Invalid MarketType. Valid values: {string.Join(", ", Enum.GetNames<MarketType>())}");

        var symbol = request.Symbol.ToUpper();

        if (await _assets.ExistsAsync(symbol))
            return Conflict($"Asset {symbol} already exists");

        var asset = new Asset { Symbol = symbol, Name = request.Name, MarketType = marketType };
        await _assets.AddAsync(asset);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new AssetDto(asset.Id, asset.Symbol, asset.Name, asset.MarketType.ToString(), asset.IsActive));
    }
}
