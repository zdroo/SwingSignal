using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SwingSignal.Domain.Entities;
using SwingSignal.Domain.Enums;
using SwingSignal.Infrastructure.ExternalClients;

namespace SwingSignal.Infrastructure.BackgroundServices;

public class MacroIngestionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MacroIngestionService> _logger;

    private static readonly MacroIndicatorType[] IndicatorsToIngest =
    [
        MacroIndicatorType.FedFundsRate,
        MacroIndicatorType.UnemploymentRate,
        MacroIndicatorType.CPI,
        MacroIndicatorType.GDP,
        MacroIndicatorType.GoldPrice,
        MacroIndicatorType.OilWTI,
        MacroIndicatorType.TreasuryYield10Y,
        MacroIndicatorType.TreasuryYield2Y,
    ];

    public MacroIngestionService(IServiceScopeFactory scopeFactory, ILogger<MacroIngestionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Run once on startup, then every 24 hours
        while (!ct.IsCancellationRequested)
        {
            await IngestAllAsync(ct);
            await Task.Delay(TimeSpan.FromHours(24), ct);
        }
    }

    private async Task IngestAllAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SwingSignalDbContext>();
        var fred = scope.ServiceProvider.GetRequiredService<FredApiClient>();

        foreach (var indicatorType in IndicatorsToIngest)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var latestDate = await db.MacroDataPoints
                    .Where(m => m.IndicatorType == indicatorType)
                    .MaxAsync(m => (DateTime?)m.Date, ct);

                var from = latestDate?.AddDays(1) ?? DateTime.UtcNow.AddYears(-5);
                var observations = await fred.GetObservationsAsync(indicatorType, from, ct);

                if (observations.Count == 0)
                {
                    _logger.LogInformation("No new data for {Indicator}", indicatorType);
                    continue;
                }

                var existingDates = (await db.MacroDataPoints
                    .Where(m => m.IndicatorType == indicatorType)
                    .Select(m => m.Date)
                    .ToListAsync(ct))
                    .ToHashSet();

                var newPoints = observations
                    .Where(o => !existingDates.Contains(o.Date))
                    .Select(o => new MacroDataPoint
                    {
                        IndicatorType = indicatorType,
                        Date = o.Date,
                        Value = o.Value,
                        Source = "FRED"
                    })
                    .ToList();

                if (newPoints.Count > 0)
                {
                    await db.MacroDataPoints.AddRangeAsync(newPoints, ct);
                    await db.SaveChangesAsync(ct);
                    _logger.LogInformation("Ingested {Count} new points for {Indicator}", newPoints.Count, indicatorType);
                }

                // Compute yield curve spread after both yields are ingested
                if (indicatorType == MacroIndicatorType.TreasuryYield2Y)
                    await ComputeYieldCurveSpreadAsync(db, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ingest {Indicator}", indicatorType);
            }
        }
    }

    private async Task ComputeYieldCurveSpreadAsync(SwingSignalDbContext db, CancellationToken ct)
    {
        var existingSpreads = (await db.MacroDataPoints
            .Where(m => m.IndicatorType == MacroIndicatorType.YieldCurveSpread)
            .Select(m => m.Date)
            .ToListAsync(ct))
            .ToHashSet();

        var yield10Y = await db.MacroDataPoints
            .Where(m => m.IndicatorType == MacroIndicatorType.TreasuryYield10Y)
            .ToDictionaryAsync(m => m.Date, m => m.Value, ct);

        var yield2Y = await db.MacroDataPoints
            .Where(m => m.IndicatorType == MacroIndicatorType.TreasuryYield2Y)
            .ToDictionaryAsync(m => m.Date, m => m.Value, ct);

        var spreads = yield10Y.Keys
            .Where(date => yield2Y.ContainsKey(date) && !existingSpreads.Contains(date))
            .Select(date => new MacroDataPoint
            {
                IndicatorType = MacroIndicatorType.YieldCurveSpread,
                Date = date,
                Value = yield10Y[date] - yield2Y[date],
                Source = "Computed"
            })
            .ToList();

        if (spreads.Count > 0)
        {
            await db.MacroDataPoints.AddRangeAsync(spreads, ct);
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Computed {Count} yield curve spread points", spreads.Count);
        }
    }
}
