using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RegimeDeck.Infrastructure.BackgroundServices;

/// A hosted service that runs a unit of work on a fixed interval, after an
/// optional startup delay, swallowing and logging per-iteration failures so
/// one bad run never stops the schedule. Subclasses supply the timing and the
/// work (RunOnceAsync) — Template Method for every periodic job.
public abstract class PeriodicBackgroundService : BackgroundService
{
    protected PeriodicBackgroundService(ILogger logger) => Logger = logger;

    protected ILogger Logger { get; }

    /// Delay before the first run. Default: run immediately.
    protected virtual TimeSpan StartupDelay => TimeSpan.Zero;

    /// Delay between the end of one run and the start of the next.
    protected abstract TimeSpan Interval { get; }

    /// One iteration of work. Exceptions are caught and logged by the base.
    protected abstract Task RunOnceAsync(CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (StartupDelay > TimeSpan.Zero && !await DelayAsync(StartupDelay, stoppingToken))
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "{Service} run failed", GetType().Name);
            }

            if (!await DelayAsync(Interval, stoppingToken))
                break;
        }
    }

    // Returns false when cancellation interrupts the delay.
    private static async Task<bool> DelayAsync(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
