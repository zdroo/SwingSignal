using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RegimeDeck.Application.Abstractions.Ingestion;
using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Application.Common;
using RegimeDeck.Application.Odds;
using RegimeDeck.Application.Screener;

namespace RegimeDeck.Infrastructure.BackgroundServices;

// Precomputes the screener board: every few hours it runs the existing odds
// engine across the curated universe and caches one row per asset. Keeping
// this off the request path is the whole point — the /screener endpoints only
// ever read the cache. Per-asset failures are swallowed so one bad symbol
// can't stall the board.
public class ScreenerComputeService : PeriodicBackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ScreenerComputeService(
        IServiceScopeFactory scopeFactory, ILogger<ScreenerComputeService> logger)
        : base(logger) => _scopeFactory = scopeFactory;

    protected override TimeSpan Interval => TimeSpan.FromHours(6);
    // Let ingestion warm up after boot so the first board uses fresh candles
    protected override TimeSpan StartupDelay => TimeSpan.FromMinutes(10);

    protected override async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var ingestion = scope.ServiceProvider.GetRequiredService<IAssetIngestionService>();
        var odds = scope.ServiceProvider.GetRequiredService<IHistoricalOddsService>();
        var repository = scope.ServiceProvider.GetRequiredService<IScreenerRepository>();

        var computed = 0;
        foreach (var symbol in ScreenerUniverse.Symbols)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var asset = await ingestion.EnsureIngestedAsync(symbol, ct);
                if (asset is null) continue; // unsupported/failed — keep any prior row

                var assetOdds = await odds.GetOddsAsync(asset.Symbol, ct);
                await repository.UpsertAsync(ScreenerRowFactory.Create(asset, assetOdds, DateTime.UtcNow), ct);
                computed++;
            }
            catch (AppException)
            {
                // A single bad asset must not abort the whole board
            }
        }

        // Warn (not just stay silent) when a whole run produces nothing — that
        // signals a systemic problem, e.g. ingestion is down.
        var total = ScreenerUniverse.Symbols.Count;
        Logger.Log(computed == 0 ? LogLevel.Warning : LogLevel.Information,
            "Screener recomputed {Count} of {Total} assets", computed, total);
    }
}
