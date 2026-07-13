using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
// ever read the cache. Mirrors AlertEvaluationService's resilience: per-asset
// failures are swallowed so one bad symbol can't stall the board.
public class ScreenerComputeService : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(6);
    // Let ingestion warm up after boot so the first board uses fresh candles
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScreenerComputeService> _logger;

    public ScreenerComputeService(
        IServiceScopeFactory scopeFactory, ILogger<ScreenerComputeService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var ct = stoppingToken;
        try
        {
            await Task.Delay(StartupDelay, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ComputeAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Screener compute run failed");
            }

            await Task.Delay(RunInterval, ct);
        }
    }

    private async Task ComputeAsync(CancellationToken ct)
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

        if (computed > 0)
            _logger.LogInformation("Screener recomputed {Count} of {Total} assets",
                computed, ScreenerUniverse.Symbols.Count);
    }
}
