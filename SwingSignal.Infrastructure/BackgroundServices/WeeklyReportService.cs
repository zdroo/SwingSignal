using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SwingSignal.Application.Abstractions.Email;
using SwingSignal.Application.Abstractions.Persistence;
using SwingSignal.Application.Regime;
using SwingSignal.Application.Reports;
using SwingSignal.Application.Watchlist;

namespace SwingSignal.Infrastructure.BackgroundServices;

// Sends the Pro weekly regime report on Mondays (UTC). The recipient query
// plus LastWeeklyReportAt makes each hourly pass idempotent — a restart
// mid-Monday resumes where it left off instead of double-sending.
public class WeeklyReportService : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WeeklyReportService> _logger;
    private readonly string _frontendUrl;

    public WeeklyReportService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<WeeklyReportService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _frontendUrl = configuration["Frontend:Url"] ?? "http://localhost:3000";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var ct = stoppingToken;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (DateTime.UtcNow.DayOfWeek == DayOfWeek.Monday)
                    await SendDueReportsAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Weekly report run failed");
            }

            await Task.Delay(RunInterval, ct);
        }
    }

    private async Task SendDueReportsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var recipients = await users.GetWeeklyReportRecipientsAsync(DateTime.UtcNow.Date, ct);
        if (recipients.Count == 0) return;

        var regimeService = scope.ServiceProvider.GetRequiredService<IMacroRegimeService>();
        var watchlist = scope.ServiceProvider.GetRequiredService<IWatchlistService>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailService>();

        // One regime computation serves every recipient
        var regime = await regimeService.GetCurrentRegimeAsync(ct);
        var subject = WeeklyReportBuilder.Subject(regime);

        foreach (var user in recipients)
        {
            if (!WeeklyReportSchedule.IsDue(DateTime.UtcNow, user.LastWeeklyReportAt)) continue;

            var rows = await watchlist.GetOverviewAsync(user.Id, ct);
            var html = WeeklyReportBuilder.BuildHtml(regime, rows, _frontendUrl);

            await email.SendWeeklyReportAsync(user.Email, subject, html, ct);

            user.LastWeeklyReportAt = DateTime.UtcNow;
            await users.UpdateAsync(user, ct);
        }

        _logger.LogInformation("Weekly report sent to {Count} Pro user(s)", recipients.Count);
    }
}
