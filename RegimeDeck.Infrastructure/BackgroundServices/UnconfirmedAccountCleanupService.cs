using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RegimeDeck.Application.Abstractions.Persistence;

namespace RegimeDeck.Infrastructure.BackgroundServices;

// Purges accounts that never confirmed their email. This keeps the user table
// (and the future mailing list) clean of throwaway/typo'd registrations, and
// means an abusive signup wave cleans itself up. Google accounts are confirmed
// at creation, so they are never touched.
public class UnconfirmedAccountCleanupService : PeriodicBackgroundService
{
    private static readonly TimeSpan GracePeriod = TimeSpan.FromDays(7);

    private readonly IServiceScopeFactory _scopeFactory;

    public UnconfirmedAccountCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<UnconfirmedAccountCleanupService> logger)
        : base(logger) => _scopeFactory = scopeFactory;

    protected override TimeSpan Interval => TimeSpan.FromHours(24);

    protected override async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var deleted = await users.DeleteUnconfirmedOlderThanAsync(DateTime.UtcNow - GracePeriod, ct);
        if (deleted > 0)
            Logger.LogInformation("Purged {Count} unconfirmed accounts older than {Days} days",
                deleted, GracePeriod.TotalDays);
    }
}
