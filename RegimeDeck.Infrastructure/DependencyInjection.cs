using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RegimeDeck.Application.Abstractions.Email;
using RegimeDeck.Application.Abstractions.Ingestion;
using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Application.Abstractions.Security;
using RegimeDeck.Application.Abstractions.Billing;
using RegimeDeck.Infrastructure.BackgroundServices;
using RegimeDeck.Infrastructure.Billing;
using RegimeDeck.Infrastructure.Email;
using RegimeDeck.Infrastructure.ExternalClients;
using RegimeDeck.Infrastructure.Ingestion;
using RegimeDeck.Infrastructure.Persistence;
using RegimeDeck.Infrastructure.Persistence.Repositories;
using RegimeDeck.Infrastructure.Security;
using RegimeDeck.Infrastructure.Seed;

namespace RegimeDeck.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration config, bool includeBackgroundServices = true)
    {
        // Persistence
        services.AddDbContext<RegimeDeckDbContext>(options =>
            options.UseSqlServer(
                config.GetConnectionString("SqlServer"),
                sql => sql.EnableRetryOnFailure())); // cloud SQL has transient faults

        services.AddScoped<IAssetRepository, AssetRepository>();
        services.AddScoped<ICandleRepository, CandleRepository>();
        services.AddScoped<IMacroRepository, MacroRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();
        services.AddScoped<IWatchlistRepository, WatchlistRepository>();
        services.AddScoped<IAlertStateRepository, AlertStateRepository>();
        services.AddScoped<IAlertRuleRepository, AlertRuleRepository>();
        services.AddScoped<IScreenerRepository, ScreenerRepository>();
        services.AddScoped<ISectorRotationRepository, SectorRotationRepository>();
        services.AddScoped<IEconomicEventRepository, EconomicEventRepository>();

        // Billing (Stripe) — endpoints reject politely while unconfigured
        services.AddScoped<IBillingService, StripeBillingService>();

        // Security
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();

        // Transactional email (Resend)
        var resendApiKey = config["Resend:ApiKey"];
        services.AddOptions();
        services.AddHttpClient<Resend.ResendClient>()
            .AddStandardResilienceHandler();
        services.Configure<Resend.ResendClientOptions>(o => o.ApiToken = resendApiKey ?? "re_missing_key");
        services.AddTransient<Resend.IResend, Resend.ResendClient>();
        services.AddScoped<IEmailService, ResendEmailService>();

        // Ingestion
        services.AddScoped<IAssetIngestionService, OnDemandIngestionService>();
        services.AddScoped<CryptoHistoryBackfillService>();
        services.AddScoped<AssetSeeder>();

        // External data providers
        services.AddHttpClient<ISymbolSearchService, YahooSymbolSearchClient>();
        services.AddHttpClient<FredApiClient>();
        services.AddHttpClient<DbNomicsApiClient>();
        services.AddHttpClient<BinanceApiClient>();
        services.AddHttpClient<CoinMetricsApiClient>();
        services.AddHttpClient<BitcoinDataApiClient>();
        services.AddHttpClient<BlockchainInfoApiClient>();
        services.AddHttpClient<YahooFinanceApiClient>();
        services.AddHttpClient<FearGreedApiClient>();

        // Scheduled ingestion (skipped in integration tests — they call external APIs)
        if (includeBackgroundServices)
        {
            services.AddHostedService<MacroIngestionService>();
            services.AddHostedService<CryptoIngestionService>();
            services.AddHostedService<StockForexIngestionService>();
            services.AddHostedService<MarketIndicatorIngestionService>();
            services.AddHostedService<UnconfirmedAccountCleanupService>();
            services.AddHostedService<WeeklyReportService>();
            services.AddHostedService<AlertEvaluationService>();
            services.AddHostedService<CustomAlertEvaluationService>();
            services.AddHostedService<ScreenerComputeService>();
            services.AddHostedService<SectorRotationComputeService>();
            services.AddHostedService<EconomicCalendarIngestionService>();
        }

        return services;
    }
}
