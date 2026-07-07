using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SwingSignal.Domain.Entities;
using SwingSignal.Infrastructure.Persistence;
using SwingSignal.Domain.Enums;

namespace SwingSignal.Infrastructure.Seed;

public class AssetSeeder
{
    private readonly SwingSignalDbContext _db;
    private readonly ILogger<AssetSeeder> _logger;

    private static readonly List<(string Symbol, string Name, MarketType Market)> DefaultAssets =
    [
        // Crypto (Binance)
        ("BTCUSDT",  "Bitcoin",          MarketType.Crypto),
        ("ETHUSDT",  "Ethereum",         MarketType.Crypto),

        // Stocks / ETFs (Yahoo Finance)
        ("SPY",      "S&P 500 ETF",      MarketType.Index),
        ("QQQ",      "NASDAQ 100 ETF",   MarketType.Index),
        ("GLD",      "Gold ETF",         MarketType.Commodity),
        ("USO",      "Oil ETF",          MarketType.Commodity),

        // Forex (Yahoo Finance - symbol format: XXXYYY=X)
        ("EURUSD=X", "EUR/USD",          MarketType.Forex),
        ("GBPUSD=X", "GBP/USD",          MarketType.Forex),
        ("USDJPY=X", "USD/JPY",          MarketType.Forex),

        // Commodities (Yahoo Finance futures)
        ("GC=F",     "Gold Futures",     MarketType.Commodity),
        ("CL=F",     "Crude Oil WTI",    MarketType.Commodity),
    ];

    public AssetSeeder(SwingSignalDbContext db, ILogger<AssetSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var existingSymbols = (await _db.Assets
            .Select(a => a.Symbol)
            .ToListAsync(ct))
            .ToHashSet();

        var toAdd = DefaultAssets
            .Where(a => !existingSymbols.Contains(a.Symbol))
            .Select(a => new Asset
            {
                Symbol     = a.Symbol,
                Name       = a.Name,
                MarketType = a.Market,
                IsActive   = true
            })
            .ToList();

        if (toAdd.Count == 0) return;

        await _db.Assets.AddRangeAsync(toAdd, ct);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Seeded {Count} default assets", toAdd.Count);
    }
}
