using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SwingSignal.Domain.Entities;
using SwingSignal.Domain.Enums;
using SwingSignal.Infrastructure.ExternalClients;
using SwingSignal.Infrastructure.Persistence;

namespace SwingSignal.Infrastructure.BackgroundServices;

public class CryptoIngestionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CryptoIngestionService> _logger;

    private static readonly CandleInterval[] Intervals =
    [
        CandleInterval.OneDay,
        CandleInterval.FourHour,
    ];

    public CryptoIngestionService(IServiceScopeFactory scopeFactory, ILogger<CryptoIngestionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await IngestAllAsync(ct);
            await Task.Delay(TimeSpan.FromHours(4), ct);
        }
    }

    private async Task IngestAllAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SwingSignalDbContext>();
        var binance = scope.ServiceProvider.GetRequiredService<BinanceApiClient>();

        var cryptoAssets = await db.Assets
            .Where(a => a.IsActive && a.MarketType == MarketType.Crypto)
            .ToListAsync(ct);

        foreach (var asset in cryptoAssets)
        {
            foreach (var interval in Intervals)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var latestTime = await db.Candles
                        .Where(c => c.AssetId == asset.Id && c.Interval == interval)
                        .MaxAsync(c => (DateTime?)c.OpenTime, ct);

                    var from = latestTime?.AddMinutes(1);
                    var rawCandles = await binance.GetCandlesAsync(asset.Symbol, interval, from, ct: ct);

                    if (rawCandles.Count == 0) continue;

                    var existingTimes = (await db.Candles
                        .Where(c => c.AssetId == asset.Id && c.Interval == interval)
                        .Select(c => c.OpenTime)
                        .ToListAsync(ct))
                        .ToHashSet();

                    var newCandles = rawCandles
                        .Where(r => !existingTimes.Contains(r.OpenTime))
                        .Select(r => new Candle
                        {
                            AssetId = asset.Id,
                            OpenTime = r.OpenTime,
                            Open = r.Open,
                            High = r.High,
                            Low = r.Low,
                            Close = r.Close,
                            Volume = r.Volume,
                            Interval = interval
                        })
                        .ToList();

                    if (newCandles.Count > 0)
                    {
                        await db.Candles.AddRangeAsync(newCandles, ct);
                        await db.SaveChangesAsync(ct);
                        _logger.LogInformation("Ingested {Count} candles for {Symbol} {Interval}",
                            newCandles.Count, asset.Symbol, interval);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to ingest {Symbol} {Interval}", asset.Symbol, interval);
                }
            }
        }
    }
}
