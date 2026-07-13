using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RegimeDeck.Application.Abstractions.Email;
using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Application.Regime;
using RegimeDeck.Application.Reports;
using RegimeDeck.Application.Watchlist;

namespace RegimeDeck.Infrastructure.BackgroundServices;

// Sends the Pro weekly regime report on Mondays (UTC). The recipient query
// plus LastWeeklyReportAt makes each hourly pass idempotent — a restart
// mid-Monday resumes where it left off instead of double-sending.
public class WeeklyReportService : PeriodicBackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _frontendUrl;

    public WeeklyReportService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<WeeklyReportService> logger)
        : base(logger)
    {
        _scopeFactory = scopeFactory;
        _frontendUrl = configuration["Frontend:Url"] ?? "http://localhost:3000";
    }

    // Hourly so a Monday is never missed; the day check + idempotency below
    // keep it to one send per user per week.
    protected override TimeSpan Interval => TimeSpan.FromHours(1);

    protected override async Task RunOnceAsync(CancellationToken ct)
    {
        if (DateTime.UtcNow.DayOfWeek == DayOfWeek.Monday)
            await SendDueReportsAsync(ct);
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
        if (regime.Health.Groups.Count == 0)
        {
            // Ingestion outage — a "Market Health 0" email would be nonsense.
            // Recipients stay unmarked, so a later pass this Monday retries.
            Logger.LogWarning("Weekly report skipped: regime has no indicator data");
            return;
        }
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

        Logger.LogInformation("Weekly report sent to {Count} Pro user(s)", recipients.Count);
    }
}
