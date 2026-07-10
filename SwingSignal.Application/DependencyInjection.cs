using Microsoft.Extensions.DependencyInjection;
using SwingSignal.Application.Analytics;
using SwingSignal.Application.Auth;
using SwingSignal.Application.Backtesting;
using SwingSignal.Application.MacroData;
using SwingSignal.Application.Markets;
using SwingSignal.Application.Odds;
using SwingSignal.Application.Regime;
using SwingSignal.Application.Watchlist;

namespace SwingSignal.Application;

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
