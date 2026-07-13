using Microsoft.Extensions.DependencyInjection;
using RegimeDeck.Application.Analytics;
using RegimeDeck.Application.Auth;
using RegimeDeck.Application.Backtesting;
using RegimeDeck.Application.MacroData;
using RegimeDeck.Application.Markets;
using RegimeDeck.Application.Odds;
using RegimeDeck.Application.Regime;
using RegimeDeck.Application.Watchlist;

namespace RegimeDeck.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMemoryCache(); // snapshot cache (idempotent if the host also registers it)

        services.AddScoped<MacroSnapshotBuilder>();
        services.AddScoped<IMacroRegimeService, MacroRegimeService>();
        services.AddScoped<IMacroQueryService, MacroQueryService>();
        services.AddScoped<IAssetExplainerService, MacroExplainerService>();
        services.AddScoped<IHistoricalOddsService, HistoricalOddsService>();
        services.AddScoped<IBacktestService, BacktestService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPopularAssetsService, PopularAssetsService>();
        services.AddScoped<IAssetCatalogService, AssetCatalogService>();
        services.AddScoped<ICandleQueryService, CandleQueryService>();
        services.AddScoped<IWaitlistService, WaitlistService>();
        services.AddScoped<ISearchLogService, SearchLogService>();
        services.AddScoped<IWatchlistService, WatchlistService>();

        return services;
    }
}
