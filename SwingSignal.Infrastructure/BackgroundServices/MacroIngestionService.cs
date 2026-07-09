using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SwingSignal.Domain.Entities;
using SwingSignal.Domain.Enums;
using SwingSignal.Infrastructure.ExternalClients;
using SwingSignal.Infrastructure.Persistence;

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
        MacroIndicatorType.OilWTI,
        MacroIndicatorType.TreasuryYield10Y,
        MacroIndicatorType.TreasuryYield2Y,
        MacroIndicatorType.TreasuryYield3M,
        MacroIndicatorType.FedBalanceSheet,
        MacroIndicatorType.ReverseRepo,
        MacroIndicatorType.RealYield10Y,
        MacroIndicatorType.M2MoneySupply,
        MacroIndicatorType.CorePCE,
        MacroIndicatorType.JoblessClaims,
        MacroIndicatorType.ConsumerSentiment,
        MacroIndicatorType.RetailSales,
        MacroIndicatorType.HousingStarts,
        MacroIndicatorType.HighYieldSpread,
        MacroIndicatorType.SahmRule,
    ];

    // Deep history gives the regime matcher more periods to compare against
    private static readonly DateTime HistoryStart = new(1990, 1, 1);

    public MacroIngestionService(IServiceScopeFactory scopeFactory, ILogger<MacroIngestionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var ct = stoppingToken;
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
        var dbNomics = scope.ServiceProvider.GetRequiredService<DbNomicsApiClient>();

        if (!fred.IsConfigured)
            _logger.LogWarning("No FRED API key configured - falling back to DBnomics (covers a subset of indicators)");

        foreach (var indicatorType in IndicatorsToIngest)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var latestDate = await db.MacroDataPoints
                    .Where(m => m.IndicatorType == indicatorType)
                    .MaxAsync(m => (DateTime?)m.Date, ct);

                var from = latestDate?.AddDays(1) ?? HistoryStart;

                var observations = fred.IsConfigured
                    ? await fred.GetObservationsAsync(indicatorType, from, ct)
                    : await dbNomics.GetObservationsAsync(indicatorType, from, ct);

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
                        Source = fred.IsConfigured ? "FRED" : "DBnomics"
                    })
                    .ToList();

                if (newPoints.Count > 0)
                {
                    await db.MacroDataPoints.AddRangeAsync(newPoints, ct);
                    await db.SaveChangesAsync(ct);
                    _logger.LogInformation("Ingested {Count} new points for {Indicator}", newPoints.Count, indicatorType);
                }

                // Compute yield spreads once the component series are ingested
                if (indicatorType == MacroIndicatorType.TreasuryYield2Y)
                    await ComputeSpreadAsync(db,
                        MacroIndicatorType.YieldCurveSpread,
                        MacroIndicatorType.TreasuryYield10Y,
                        MacroIndicatorType.TreasuryYield2Y, ct);

                if (indicatorType == MacroIndicatorType.TreasuryYield3M)
                    await ComputeSpreadAsync(db,
                        MacroIndicatorType.YieldSpread10Y3M,
                        MacroIndicatorType.TreasuryYield10Y,
                        MacroIndicatorType.TreasuryYield3M, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ingest {Indicator}", indicatorType);
            }
        }
    }

    private async Task ComputeSpreadAsync(
        SwingSignalDbContext db,
        MacroIndicatorType spreadType,
        MacroIndicatorType longType,
        MacroIndicatorType shortType,
        CancellationToken ct)
    {
        var existingSpreads = (await db.MacroDataPoints
            .Where(m => m.IndicatorType == spreadType)
            .Select(m => m.Date)
            .ToListAsync(ct))
            .ToHashSet();

        var longSeries = await db.MacroDataPoints
            .Where(m => m.IndicatorType == longType)
            .ToDictionaryAsync(m => m.Date, m => m.Value, ct);

        var shortSeries = await db.MacroDataPoints
            .Where(m => m.IndicatorType == shortType)
            .ToDictionaryAsync(m => m.Date, m => m.Value, ct);

        var spreads = longSeries.Keys
            .Where(date => shortSeries.ContainsKey(date) && !existingSpreads.Contains(date))
            .Select(date => new MacroDataPoint
            {
                IndicatorType = spreadType,
                Date = date,
                Value = longSeries[date] - shortSeries[date],
                Source = "Computed"
            })
            .ToList();

        if (spreads.Count > 0)
        {
            await db.MacroDataPoints.AddRangeAsync(spreads, ct);
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Computed {Count} {SpreadType} points", spreads.Count, spreadType);
        }
    }
}
