using Microsoft.Extensions.DependencyInjection;
using SwingSignal.Application.Auth;
using SwingSignal.Application.Backtesting;
using SwingSignal.Application.Markets;
using SwingSignal.Application.Odds;
using SwingSignal.Application.Regime;

namespace SwingSignal.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<MacroSnapshotBuilder>();
        services.AddScoped<IMacroRegimeService, MacroRegimeService>();
        services.AddScoped<IAssetExplainerService, MacroExplainerService>();
        services.AddScoped<IHistoricalOddsService, HistoricalOddsService>();
        services.AddScoped<IBacktestService, BacktestService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPopularAssetsService, PopularAssetsService>();

        return services;
    }
}
