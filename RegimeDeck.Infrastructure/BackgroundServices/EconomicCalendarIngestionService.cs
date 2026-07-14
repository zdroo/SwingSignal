using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Application.Events;
using RegimeDeck.Domain.Entities;
using RegimeDeck.Infrastructure.ExternalClients;

namespace RegimeDeck.Infrastructure.BackgroundServices;

// Refreshes the macro-event calendar daily: pulls each curated release's
// upcoming FRED dates and upserts them, then drops past occurrences. The
// /events endpoint only ever reads the cache. Fail-soft — a FRED hiccup for
// one release just leaves its existing rows in place.
public class EconomicCalendarIngestionService : PeriodicBackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public EconomicCalendarIngestionService(
        IServiceScopeFactory scopeFactory, ILogger<EconomicCalendarIngestionService> logger)
        : base(logger) => _scopeFactory = scopeFactory;

    protected override TimeSpan Interval => TimeSpan.FromHours(24);
    // A short delay so it runs shortly after boot rather than at the same
    // instant as every other startup task
    protected override TimeSpan StartupDelay => TimeSpan.FromMinutes(2);

    protected override async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var fred = scope.ServiceProvider.GetRequiredService<FredApiClient>();
        var repository = scope.ServiceProvider.GetRequiredService<IEconomicEventRepository>();

        if (!fred.IsConfigured) return; // no key → leave the calendar as-is

        var today = DateTime.UtcNow.Date;
        var upserted = 0;

        foreach (var (releaseId, title) in EconomicCalendar.Releases)
        {
            if (ct.IsCancellationRequested) break;

            var dates = await fred.GetReleaseDatesAsync(releaseId, today, ct);
            foreach (var date in dates)
            {
                await repository.UpsertAsync(new EconomicEvent
                {
                    ReleaseId = releaseId,
                    Title = title,
                    Date = date,
                    Impact = EconomicCalendar.HighImpact,
                }, ct);
                upserted++;
            }
        }

        await repository.DeleteBeforeAsync(today, ct);

        if (upserted > 0)
            Logger.LogInformation("Economic calendar refreshed: {Count} upcoming release dates", upserted);
    }
}
