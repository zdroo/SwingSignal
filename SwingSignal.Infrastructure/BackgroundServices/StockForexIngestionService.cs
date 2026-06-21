using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SwingSignal.Domain.Entities;
using SwingSignal.Domain.Enums;
using SwingSignal.Infrastructure.ExternalClients;

namespace SwingSignal.Infrastructure.BackgroundServices;

public class StockForexIngestionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StockForexIngestionService> _logger;

    private static readonly MarketType[] SupportedMarkets =
    [
        MarketType.Stock,
        MarketType.Forex,
        MarketType.Commodity,
        MarketType.Index
    ];

    public StockForexIngestionService(IServiceScopeFactory scopeFactory, ILogger<StockForexIngestionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await IngestAllAsync(ct);
            await Task.Delay(TimeSpan.FromHours(12), ct);
        }
    }

    private async Task IngestAllAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SwingSignalDbContext>();
        var yahoo = scope.ServiceProvider.GetRequiredService<YahooFinanceApiClient>();

        var assets = await db.Assets
            .Where(a => a.IsActive && SupportedMarkets.Contains(a.MarketType))
            .ToListAsync(ct);

        foreach (var asset in assets)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var latestTime = await db.Candles
                    .Where(c => c.AssetId == asset.Id && c.Interval == CandleInterval.OneDay)
                    .MaxAsync(c => (DateTime?)c.OpenTime, ct);

                var from = latestTime?.AddDays(1);

                // Small delay between requests to avoid Yahoo rate limiting
                await Task.Delay(TimeSpan.FromSeconds(2), ct);

                var rawCandles = await yahoo.GetCandlesAsync(
                    asset.Symbol, CandleInterval.OneDay, from, ct);

                if (rawCandles.Count == 0) continue;

                var existingTimes = (await db.Candles
                    .Where(c => c.AssetId == asset.Id && c.Interval == CandleInterval.OneDay)
                    .Select(c => c.OpenTime)
                    .ToListAsync(ct))
                    .ToHashSet();

                var newCandles = rawCandles
                    .Where(r => !existingTimes.Contains(r.OpenTime))
                    .Select(r => new Candle
                    {
                        AssetId  = asset.Id,
                        OpenTime = r.OpenTime,
                        Open     = r.Open,
                        High     = r.High,
                        Low      = r.Low,
                        Close    = r.Close,
                        Volume   = r.Volume,
                        Interval = CandleInterval.OneDay
                    })
                    .ToList();

                if (newCandles.Count > 0)
                {
                    await db.Candles.AddRangeAsync(newCandles, ct);
                    await db.SaveChangesAsync(ct);
                    _logger.LogInformation("Ingested {Count} candles for {Symbol}",
                        newCandles.Count, asset.Symbol);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ingest {Symbol}", asset.Symbol);
            }
        }
    }
}
