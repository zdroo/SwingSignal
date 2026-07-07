using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SwingSignal.Application.Abstractions.Ingestion;
using SwingSignal.Application.Abstractions.Persistence;
using SwingSignal.Application.Abstractions.Security;
using SwingSignal.Infrastructure.BackgroundServices;
using SwingSignal.Infrastructure.ExternalClients;
using SwingSignal.Infrastructure.Ingestion;
using SwingSignal.Infrastructure.Persistence;
using SwingSignal.Infrastructure.Persistence.Repositories;
using SwingSignal.Infrastructure.Security;
using SwingSignal.Infrastructure.Seed;

namespace SwingSignal.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // Persistence
        services.AddDbContext<SwingSignalDbContext>(options =>
            options.UseSqlServer(config.GetConnectionString("SqlServer")));

        services.AddScoped<IAssetRepository, AssetRepository>();
        services.AddScoped<ICandleRepository, CandleRepository>();
        services.AddScoped<IMacroRepository, MacroRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        // Security
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();

        // Ingestion
        services.AddScoped<IAssetIngestionService, OnDemandIngestionService>();
        services.AddScoped<AssetSeeder>();

        // External data providers
        services.AddHttpClient<ISymbolSearchService, YahooSymbolSearchClient>();
        services.AddHttpClient<FredApiClient>();
        services.AddHttpClient<DbNomicsApiClient>();
        services.AddHttpClient<BinanceApiClient>();
        services.AddHttpClient<YahooFinanceApiClient>();
        services.AddHttpClient<FearGreedApiClient>();

        // Scheduled ingestion
        services.AddHostedService<MacroIngestionService>();
        services.AddHostedService<CryptoIngestionService>();
        services.AddHostedService<StockForexIngestionService>();
        services.AddHostedService<MarketIndicatorIngestionService>();

        return services;
    }
}
