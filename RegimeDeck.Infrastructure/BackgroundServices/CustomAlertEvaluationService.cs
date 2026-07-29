using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RegimeDeck.Application.Abstractions.Email;
using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Application.Alerts;
using RegimeDeck.Contracts.Alerts;
using RegimeDeck.Domain.Entities;
using RegimeDeck.Domain.Enums;

namespace RegimeDeck.Infrastructure.BackgroundServices;

// Evaluates every user's custom alert rules against the latest daily readings.
// Edge-triggered: a rule fires once when its AND-combined conditions first all
// hold, then re-arms only after they stop holding (LastMet). Daily-close data,
// not real-time.
public class CustomAlertEvaluationService : PeriodicBackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _frontendUrl;

    public CustomAlertEvaluationService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<CustomAlertEvaluationService> logger)
        : base(logger)
    {
        _scopeFactory = scopeFactory;
        _frontendUrl = configuration["Frontend:Url"] ?? "http://localhost:3000";
    }

    protected override TimeSpan Interval => TimeSpan.FromHours(3);
    protected override TimeSpan StartupDelay => TimeSpan.FromMinutes(25);

    protected override Task RunOnceAsync(CancellationToken ct) => EvaluateAsync(ct);

    private async Task EvaluateAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var rulesRepo = scope.ServiceProvider.GetRequiredService<IAlertRuleRepository>();

        var owned = await rulesRepo.GetEnabledWithOwnerAsync(ct);
        if (owned.Count == 0) return;

        var macroRepo = scope.ServiceProvider.GetRequiredService<IMacroRepository>();
        var candleRepo = scope.ServiceProvider.GetRequiredService<ICandleRepository>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var parsed = owned
            .Select(o => (o.Rule, o.Email, Conditions: Deserialize(o.Rule.ConditionsJson)))
            .ToList();

        var data = await GatherAsync(parsed.SelectMany(p => p.Conditions), macroRepo, candleRepo, ct);

        var perOwner = new Dictionary<string, List<(string Name, string Summary)>>(StringComparer.OrdinalIgnoreCase);
        var changed = false;

        foreach (var (rule, ownerEmail, conditions) in parsed)
        {
            var met = AlertConditionEvaluator.RuleMet(conditions, data);
            if (met is null) continue; // not evaluable this run — leave state untouched

            if (met.Value && !rule.LastMet)
            {
                rule.LastMet = true;
                rule.LastTriggeredAt = DateTime.UtcNow;
                changed = true;

                if (!perOwner.TryGetValue(ownerEmail, out var list)) perOwner[ownerEmail] = list = [];
                list.Add((rule.Name, AlertRuleSummary.Describe(conditions)));
            }
            else if (!met.Value && rule.LastMet)
            {
                rule.LastMet = false; // re-arm
                changed = true;
            }
        }

        if (changed) await rulesRepo.SaveChangesAsync(ct);

        var sent = 0;
        foreach (var (ownerEmail, fired) in perOwner)
        {
            if (fired.Count == 0) continue;
            await email.SendAlertAsync(
                ownerEmail, CustomAlertEmailBuilder.Subject(fired.Count),
                CustomAlertEmailBuilder.BuildHtml(fired, _frontendUrl), ct);
            sent++;
        }

        if (sent > 0)
            Logger.LogInformation("Custom alert digests sent to {Count} user(s)", sent);
    }

    private static async Task<AlertData> GatherAsync(
        IEnumerable<AlertConditionDto> conditions, IMacroRepository macroRepo, ICandleRepository candleRepo, CancellationToken ct)
    {
        var macroKeys = new HashSet<string>();
        var symbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in conditions)
        {
            if (c.Type == AlertConditionTypes.MacroIndicator) macroKeys.Add(c.Subject);
            else symbols.Add(c.Subject);
        }

        var macro = new Dictionary<string, double>();
        foreach (var key in macroKeys)
            if (Enum.TryParse<MacroIndicatorType>(key, out var type))
            {
                var latest = await macroRepo.GetLatestAsync(type, ct);
                if (latest is not null) macro[key] = (double)latest.Value;
            }

        var candles = new Dictionary<string, List<Candle>>(StringComparer.OrdinalIgnoreCase);
        foreach (var symbol in symbols)
        {
            var series = await candleRepo.GetBySymbolAsync(symbol, CandleInterval.OneDay, 260, ct);
            if (series.Count > 0) candles[symbol] = [.. series.OrderBy(x => x.OpenTime)];
        }

        return new AlertData { Macro = macro, Candles = candles };
    }

    private static List<AlertConditionDto> Deserialize(string json) =>
        JsonSerializer.Deserialize<List<AlertConditionDto>>(json) ?? [];
}
