using Microsoft.Extensions.DependencyInjection;
using RegimeDeck.Application.Alerts;
using RegimeDeck.Application.Analytics;
using RegimeDeck.Application.Auth;
using RegimeDeck.Application.Backtesting;
using RegimeDeck.Application.Liquidity;
using RegimeDeck.Application.MacroData;
using RegimeDeck.Application.Markets;
using RegimeDeck.Application.Portfolio;
using RegimeDeck.Application.Odds;
using RegimeDeck.Application.Events;
using RegimeDeck.Application.Regime;
using RegimeDeck.Application.Screener;
using RegimeDeck.Application.Sectors;
using RegimeDeck.Application.Watchlist;

namespace RegimeDeck.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMemoryCache(); // snapshot cache (idempotent if the host also registers it)

        services.AddScoped<MacroSnapshotBuilder>();
        services.AddScoped<IMacroRegimeService, MacroRegimeService>();
        services.AddScoped<IRegimePlaybookService, RegimePlaybookService>();
        services.AddScoped<ILiquidityService, LiquidityService>();
        services.AddScoped<IPortfolioXrayService, PortfolioXrayService>();
        services.AddScoped<IAlertRuleService, AlertRuleService>();
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
        services.AddScoped<IScreenerService, ScreenerService>();
        services.AddScoped<ISectorRotationService, SectorRotationService>();
        services.AddScoped<IEconomicCalendarService, EconomicCalendarService>();

        return services;
    }
}
