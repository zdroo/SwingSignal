using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RegimeDeck.Application.Abstractions.Email;
using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Application.Alerts;
using RegimeDeck.Application.Common;
using RegimeDeck.Application.Odds;
using RegimeDeck.Application.Regime;
using RegimeDeck.Contracts.Regime;

namespace RegimeDeck.Infrastructure.BackgroundServices;

// Evaluates alert conditions every few hours: the market-health band for
// everyone, and the statistical-read stance for every symbol on any Pro
// user's watchlist. Each user gets at most one digest email per run.
// State lives in AlertStates, so a restart never re-fires old changes.
public class AlertEvaluationService : PeriodicBackgroundService
{
    // Hysteresis: a state must have held for most of a day before its flip
    // is news — a value flapping at a threshold updates silently until it
    // stabilizes, instead of emailing on every oscillation
    private static readonly TimeSpan MinStableAge = TimeSpan.FromHours(20);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _frontendUrl;

    public AlertEvaluationService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<AlertEvaluationService> logger)
        : base(logger)
    {
        _scopeFactory = scopeFactory;
        _frontendUrl = configuration["Frontend:Url"] ?? "http://localhost:3000";
    }

    protected override TimeSpan Interval => TimeSpan.FromHours(4);
    // Give ingestion a head start after boot so we compare fresh data
    protected override TimeSpan StartupDelay => TimeSpan.FromMinutes(20);

    protected override Task RunOnceAsync(CancellationToken ct) => EvaluateAsync(ct);

    private async Task EvaluateAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var recipients = await users.GetAlertRecipientsAsync(ct);
        if (recipients.Count == 0) return;

        var states = scope.ServiceProvider.GetRequiredService<IAlertStateRepository>();
        var regime = scope.ServiceProvider.GetRequiredService<IMacroRegimeService>();
        var watchlists = scope.ServiceProvider.GetRequiredService<IWatchlistRepository>();
        var odds = scope.ServiceProvider.GetRequiredService<IHistoricalOddsService>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var stored = (await states.GetAllAsync(ct)).ToDictionary(a => a.Key);
        var perUser = recipients.ToDictionary(u => u.Id, _ => new List<AlertRules.Change>());

        string? PreviousValue(string key) =>
            stored.TryGetValue(key, out var s) ? s.Value : null;
        bool PreviousIsStable(string key) =>
            stored.TryGetValue(key, out var s) && s.UpdatedAt <= DateTime.UtcNow - MinStableAge;

        // Market health — one check, everyone subscribed
        var current = await regime.GetCurrentRegimeAsync(ct);
        var healthChange = AlertRules.HealthChange(PreviousValue(AlertRules.HealthKey), current.Health);
        if (healthChange is not null)
        {
            var notify = healthChange.Notify && PreviousIsStable(healthChange.Key);
            await states.UpsertAsync(healthChange.Key, healthChange.NewValue, ct);
            if (notify)
                foreach (var changes in perUser.Values)
                    changes.Add(healthChange);
        }

        // Watchlist stances — one odds computation per distinct symbol
        var symbolSubscribers = new Dictionary<string, List<Guid>>(StringComparer.OrdinalIgnoreCase);
        foreach (var user in recipients)
        {
            foreach (var item in await watchlists.GetByUserAsync(user.Id, ct))
            {
                if (!symbolSubscribers.TryGetValue(item.Symbol, out var subs))
                    symbolSubscribers[item.Symbol] = subs = [];
                subs.Add(user.Id);
            }
        }

        foreach (var (symbol, subscribers) in symbolSubscribers)
        {
            AssetOddsDto assetOdds;
            try
            {
                assetOdds = await odds.GetOddsAsync(symbol, ct);
            }
            catch (AppException)
            {
                continue; // one broken asset must not kill the whole run
            }

            var threeMonths = assetOdds.ThreeMonths;
            var summary = ThreeMonthSummary.From(assetOdds);

            // All three watchlist triggers read the one odds computation; each
            // owns its own state key, hysteresis and notify rule.
            AlertRules.Change?[] candidates =
            [
                AlertRules.StanceChange(
                    symbol, PreviousValue(AlertRules.StanceKey(symbol)), assetOdds.TradeRead),
                AlertRules.EdgeChange(
                    symbol, PreviousValue(AlertRules.EdgeKey(symbol)), summary.Edge3M),
                AlertRules.PriceZoneChange(
                    symbol, PreviousValue(AlertRules.PriceZoneKey(symbol)),
                    assetOdds.CurrentPrice, threeMonths.PriceTargetLow, threeMonths.PriceTargetHigh),
            ];

            foreach (var change in candidates)
            {
                if (change is null) continue;

                var notifySubscribers = change.Notify && PreviousIsStable(change.Key);
                await states.UpsertAsync(change.Key, change.NewValue, ct);
                if (!notifySubscribers) continue;

                foreach (var userId in subscribers)
                    perUser[userId].Add(change);
            }
        }

        // One digest per user
        var sent = 0;
        foreach (var user in recipients)
        {
            var changes = perUser[user.Id];
            if (changes.Count == 0) continue;

            await email.SendAlertAsync(
                user.Email,
                AlertEmailBuilder.Subject(changes),
                AlertEmailBuilder.BuildHtml(changes, _frontendUrl),
                ct);
            sent++;
        }

        if (sent > 0)
            Logger.LogInformation("Alert digests sent to {Count} user(s)", sent);
    }
}
