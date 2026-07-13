using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RegimeDeck.Application.Abstractions.Persistence;

namespace RegimeDeck.Infrastructure.BackgroundServices;

// Purges accounts that never confirmed their email. This keeps the user table
// (and the future mailing list) clean of throwaway/typo'd registrations, and
// means an abusive signup wave cleans itself up. Google accounts are confirmed
// at creation, so they are never touched.
public class UnconfirmedAccountCleanupService : BackgroundService
{
    private static readonly TimeSpan GracePeriod = TimeSpan.FromDays(7);
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UnconfirmedAccountCleanupService> _logger;

    public UnconfirmedAccountCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<UnconfirmedAccountCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var ct = stoppingToken;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();

                var deleted = await users.DeleteUnconfirmedOlderThanAsync(DateTime.UtcNow - GracePeriod, ct);
                if (deleted > 0)
                    _logger.LogInformation("Purged {Count} unconfirmed accounts older than {Days} days",
                        deleted, GracePeriod.TotalDays);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unconfirmed account cleanup failed");
            }

            await Task.Delay(RunInterval, ct);
        }
    }
}
