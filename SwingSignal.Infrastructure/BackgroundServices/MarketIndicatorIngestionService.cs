using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SwingSignal.Domain.Entities;
using SwingSignal.Domain.Enums;
using SwingSignal.Infrastructure.ExternalClients;
using SwingSignal.Infrastructure.Persistence;

namespace SwingSignal.Infrastructure.BackgroundServices;

// Ingests market-derived macro indicators (VIX, DXY, Copper, Gold) from Yahoo Finance
// and the Crypto Fear & Greed index. These are stored as MacroDataPoints (daily close),
// not as tradable assets.
public class MarketIndicatorIngestionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MarketIndicatorIngestionService> _logger;

    private static readonly Dictionary<MacroIndicatorType, string> YahooSymbols = new()
    {
        [MacroIndicatorType.VIX]         = "^VIX",
        [MacroIndicatorType.DollarIndex] = "DX-Y.NYB",
        [MacroIndicatorType.Copper]      = "HG=F",
        [MacroIndicatorType.GoldPrice]   = "GC=F",
    };

    public MarketIndicatorIngestionService(
        IServiceScopeFactory scopeFactory,
        ILogger<MarketIndicatorIngestionService> logger)
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
        var fearGreed = scope.ServiceProvider.GetRequiredService<FearGreedApiClient>();

        foreach (var (indicatorType, symbol) in YahooSymbols)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var latestDate = await db.MacroDataPoints
                    .Where(m => m.IndicatorType == indicatorType)
                    .MaxAsync(m => (DateTime?)m.Date, ct);

                var from = latestDate?.AddDays(1) ?? DateTime.UtcNow.AddYears(-30);
                var candles = await yahoo.GetCandlesAsync(symbol, CandleInterval.OneDay, from, ct);

                if (candles.Count == 0) continue;

                await SavePointsAsync(
                    db, indicatorType,
                    candles.Select(c => (c.OpenTime.Date, c.Close)),
                    "YahooFinance", ct);

                // Be polite to Yahoo between symbols
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ingest market indicator {Indicator}", indicatorType);
            }
        }

        try
        {
            var history = await fearGreed.GetHistoryAsync(ct);
            if (history.Count > 0)
            {
                await SavePointsAsync(
                    db, MacroIndicatorType.CryptoFearGreed,
                    history.Select(h => (h.Date, h.Value)),
                    "Alternative.me", ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ingest Fear & Greed index");
        }
    }

    private async Task SavePointsAsync(
        SwingSignalDbContext db,
        MacroIndicatorType indicatorType,
        IEnumerable<(DateTime Date, decimal Value)> points,
        string source,
        CancellationToken ct)
    {
        var existingDates = (await db.MacroDataPoints
            .Where(m => m.IndicatorType == indicatorType)
            .Select(m => m.Date)
            .ToListAsync(ct))
            .ToHashSet();

        var newPoints = points
            .GroupBy(p => p.Date)
            .Select(g => g.Last())
            .Where(p => !existingDates.Contains(p.Date))
            .Select(p => new MacroDataPoint
            {
                IndicatorType = indicatorType,
                Date = p.Date,
                Value = p.Value,
                Source = source
            })
            .ToList();

        if (newPoints.Count > 0)
        {
            await db.MacroDataPoints.AddRangeAsync(newPoints, ct);
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Ingested {Count} points for {Indicator}", newPoints.Count, indicatorType);
        }
    }
}
