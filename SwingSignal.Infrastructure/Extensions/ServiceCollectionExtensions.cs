using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SwingSignal.Application.Interfaces;
using SwingSignal.Infrastructure.BackgroundServices;
using SwingSignal.Infrastructure.ExternalClients;
using SwingSignal.Infrastructure.Repositories;
using SwingSignal.Infrastructure.Seed;
using SwingSignal.Infrastructure.Services;

namespace SwingSignal.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<SwingSignalDbContext>(options =>
            options.UseSqlServer(config.GetConnectionString("SqlServer")));

        services.AddScoped<IAssetRepository, AssetRepository>();
        services.AddScoped<ICandleRepository, CandleRepository>();
        services.AddScoped<IMacroRepository, MacroRepository>();
        services.AddScoped<IMacroRegimeService, MacroRegimeService>();
        services.AddScoped<IHistoricalOddsService, HistoricalOddsService>();
        services.AddScoped<AssetSeeder>();

        services.AddHttpClient<FredApiClient>();
        services.AddHttpClient<BinanceApiClient>();
        services.AddHttpClient<YahooFinanceApiClient>();

        services.AddHostedService<MacroIngestionService>();
        services.AddHostedService<CryptoIngestionService>();
        services.AddHostedService<StockForexIngestionService>();

        return services;
    }
}
