using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SwingSignal.Application.Abstractions.Email;
using SwingSignal.Application.Abstractions.Ingestion;
using SwingSignal.Application.Common;
using SwingSignal.Domain.Entities;
using SwingSignal.Infrastructure.Persistence;

namespace SwingSignal.Tests.Integration;

/// Boots the real API in the Testing environment: real middleware, auth,
/// gates and controllers — with SQLite in-memory instead of SQL Server and
/// stubs for everything that would leave the machine (email, ingestion).
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    /// Override to boot with the Pro dark-launch flag ON (default mirrors
    /// production today: off).
    protected virtual bool ProEnabled => false;

    /// Override to configure a Stripe webhook secret (keys absent from
    /// appsettings.json, so an in-memory override sticks here).
    protected virtual string? StripeWebhookSecret => null;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "integration-test-secret-0123456789abcdefghijklmnop",
                ["Jwt:Issuer"] = "SwingSignal",
                ["Jwt:Audience"] = "SwingSignalWeb",
                ["Google:ClientId"] = "integration-test-client-id",
                ["Frontend:Url"] = "http://localhost:3000",
            };
            if (StripeWebhookSecret is not null)
            {
                settings["Stripe:SecretKey"] = "sk_test_integration";
                settings["Stripe:WebhookSecret"] = StripeWebhookSecret;
            }
            config.AddInMemoryCollection(settings);
        });

        builder.ConfigureServices(services =>
        {
            // Swap SQL Server for a shared in-memory SQLite database
            services.RemoveAll<DbContextOptions<SwingSignalDbContext>>();
            _connection.Open();
            services.AddDbContext<SwingSignalDbContext>(o => o.UseSqlite(_connection));

            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService, NoopEmailService>();

            services.RemoveAll<IAssetIngestionService>();
            services.AddScoped<IAssetIngestionService, StubIngestionService>();

            // Replace the singleton rather than injecting config: appsettings.json
            // loads after test config in the deferred host, so a config override
            // for Features:ProEnabled would silently lose
            if (ProEnabled)
            {
                services.RemoveAll<ProFeatures>();
                services.AddSingleton(new ProFeatures(enabled: true));
            }
        });
    }

    /// Creates the schema once the host exists (Program skips Migrate in Testing).
    public void EnsureSchema()
    {
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<SwingSignalDbContext>().Database.EnsureCreated();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }

    private sealed class NoopEmailService : IEmailService
    {
        public Task SendWelcomeAsync(string toEmail, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendEmailConfirmationAsync(string toEmail, string confirmUrl, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendPasswordResetAsync(string toEmail, string resetUrl, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendWeeklyReportAsync(string toEmail, string subject, string bodyHtml, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendAlertAsync(string toEmail, string subject, string bodyHtml, CancellationToken ct = default) => Task.CompletedTask;
    }

    /// Registers assets without touching Binance/Yahoo. The magic symbol
    /// FAILUSDT simulates an unsupported symbol (ingestion returns null → 503).
    private sealed class StubIngestionService : IAssetIngestionService
    {
        private readonly SwingSignalDbContext _db;

        public StubIngestionService(SwingSignalDbContext db) => _db = db;

        public async Task<Asset?> EnsureIngestedAsync(string symbol, CancellationToken ct = default)
        {
            if (symbol == "FAILUSDT") return null;

            var existing = await _db.Assets.FirstOrDefaultAsync(a => a.Symbol == symbol, ct);
            if (existing is not null) return existing;

            var asset = new Asset
            {
                Symbol = symbol,
                Name = symbol,
                MarketType = SymbolNormalizer.DetectMarketType(symbol),
                IsActive = true,
            };
            _db.Assets.Add(asset);
            await _db.SaveChangesAsync(ct);
            return asset;
        }
    }
}
