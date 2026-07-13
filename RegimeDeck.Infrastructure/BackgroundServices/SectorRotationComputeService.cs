using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RegimeDeck.Application.Abstractions.Ingestion;
using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Application.Common;
using RegimeDeck.Application.Odds;
using RegimeDeck.Application.Sectors;

namespace RegimeDeck.Infrastructure.BackgroundServices;

// Precomputes the sector board every few hours: the odds engine gives each
// sector ETF its regime fit, and its candles vs the benchmark's give relative
// strength. Self-contained — it does not touch the screener's cache or
// universe, only the shared odds/candle services. Per-sector failures are
// swallowed so one bad sector can't stall the board.
public class SectorRotationComputeService : PeriodicBackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SectorRotationComputeService(
        IServiceScopeFactory scopeFactory, ILogger<SectorRotationComputeService> logger)
        : base(logger) => _scopeFactory = scopeFactory;

    protected override TimeSpan Interval => TimeSpan.FromHours(6);
    protected override TimeSpan StartupDelay => TimeSpan.FromMinutes(12);

    protected override async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var ingestion = scope.ServiceProvider.GetRequiredService<IAssetIngestionService>();
        var odds = scope.ServiceProvider.GetRequiredService<IHistoricalOddsService>();
        var candles = scope.ServiceProvider.GetRequiredService<ICandleRepository>();
        var repository = scope.ServiceProvider.GetRequiredService<ISectorRotationRepository>();

        // Benchmark candles once — relative strength is meaningless without them
        var benchmark = await ingestion.EnsureIngestedAsync(SectorUniverse.Benchmark, ct);
        if (benchmark is null) return;
        var benchmarkCandles = await candles.GetDailyHistoryAsync(benchmark.Id, ct);

        var computed = 0;
        foreach (var (symbol, name) in SectorUniverse.Sectors)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var asset = await ingestion.EnsureIngestedAsync(symbol, ct);
                if (asset is null) continue;

                var assetOdds = await odds.GetOddsAsync(asset.Symbol, ct);
                var sectorCandles = await candles.GetDailyHistoryAsync(asset.Id, ct);
                var relStrength = RelativeStrength.Compute(
                    sectorCandles, benchmarkCandles, SectorUniverse.RelStrengthLookbackDays);

                await repository.UpsertAsync(
                    SectorRotationRowFactory.Create(symbol, name, assetOdds, relStrength, DateTime.UtcNow), ct);
                computed++;
            }
            catch (AppException)
            {
                // A single bad sector must not abort the whole board
            }
        }

        if (computed > 0)
            Logger.LogInformation("Sector rotation recomputed {Count} of {Total} sectors",
                computed, SectorUniverse.Sectors.Count);
    }
}
