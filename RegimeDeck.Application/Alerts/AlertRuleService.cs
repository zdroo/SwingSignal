using System.Text.Json;
using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Application.Common;
using RegimeDeck.Contracts.Alerts;
using RegimeDeck.Domain.Entities;
using RegimeDeck.Domain.Enums;

namespace RegimeDeck.Application.Alerts;

public class AlertRuleService : IAlertRuleService
{
    public const int MaxRules = 10;
    public const int MaxConditions = 5;

    private readonly IAlertRuleRepository _rules;

    public AlertRuleService(IAlertRuleRepository rules) => _rules = rules;

    public async Task<List<AlertRuleDto>> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var rules = await _rules.GetByUserAsync(userId, ct);
        return [.. rules.Select(ToDto)];
    }

    public async Task<AlertRuleDto> CreateAsync(Guid userId, CreateAlertRuleRequest request, CancellationToken ct = default)
    {
        var name = CleanName(request.Name);
        var conditions = ValidateAndNormalize(request.Conditions);

        if (await _rules.CountByUserAsync(userId, ct) >= MaxRules)
            throw new ValidationException($"You've reached the limit of {MaxRules} alerts. Delete one to add another.");

        var rule = new AlertRule
        {
            UserId = userId,
            Name = name,
            Enabled = true,
            ConditionsJson = JsonSerializer.Serialize(conditions),
        };
        await _rules.AddAsync(rule, ct);
        return ToDto(rule);
    }

    public async Task<AlertRuleDto> UpdateAsync(Guid userId, Guid id, UpdateAlertRuleRequest request, CancellationToken ct = default)
    {
        var rule = await OwnedRuleAsync(userId, id, ct);

        if (request.Name is not null)
            rule.Name = CleanName(request.Name);

        if (request.Enabled is bool enabled)
            rule.Enabled = enabled;

        if (request.Conditions is not null)
        {
            rule.ConditionsJson = JsonSerializer.Serialize(ValidateAndNormalize(request.Conditions));
            rule.LastMet = false; // conditions changed — rearm
        }

        await _rules.SaveChangesAsync(ct);
        return ToDto(rule);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken ct = default) =>
        await _rules.DeleteAsync(await OwnedRuleAsync(userId, id, ct), ct);

    private async Task<AlertRule> OwnedRuleAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var rule = await _rules.GetByIdAsync(id, ct);
        if (rule is null || rule.UserId != userId)
            throw new NotFoundException("Alert not found.");
        return rule;
    }

    private static string CleanName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed)) throw new ValidationException("Give the alert a name.");
        return trimmed.Length > 100 ? trimmed[..100] : trimmed;
    }

    private static List<AlertConditionDto> ValidateAndNormalize(List<AlertConditionDto>? conditions)
    {
        if (conditions is null || conditions.Count == 0)
            throw new ValidationException("Add at least one condition.");
        if (conditions.Count > MaxConditions)
            throw new ValidationException($"Up to {MaxConditions} conditions per alert.");

        return [.. conditions.Select(Normalize)];
    }

    private static AlertConditionDto Normalize(AlertConditionDto c)
    {
        if (!AlertConditionTypes.All.Contains(c.Type))
            throw new ValidationException($"Unknown condition type '{c.Type}'.");
        if (!AlertOperators.All.Contains(c.Operator))
            throw new ValidationException("Operator must be Above or Below.");
        if (string.IsNullOrWhiteSpace(c.Subject))
            throw new ValidationException("Each condition needs a subject.");
        if (double.IsNaN(c.Threshold) || double.IsInfinity(c.Threshold))
            throw new ValidationException("Invalid threshold.");

        return c.Type switch
        {
            AlertConditionTypes.MacroIndicator => c with { Subject = MacroName(c.Subject) },
            AlertConditionTypes.AssetPrice => RequirePositive(c) with { Subject = SymbolNormalizer.Normalize(c.Subject) },
            AlertConditionTypes.MovingAverage => RequireParam(c, 5, 400) with { Subject = SymbolNormalizer.Normalize(c.Subject) },
            AlertConditionTypes.VolumeSpike => RequirePositive(RequireParam(c, 5, 90)) with { Subject = SymbolNormalizer.Normalize(c.Subject) },
            _ => throw new ValidationException($"Unknown condition type '{c.Type}'."),
        };
    }

    private static string MacroName(string subject) =>
        Enum.TryParse<MacroIndicatorType>(subject, ignoreCase: true, out var type)
            ? type.ToString()
            : throw new ValidationException($"Unknown macro indicator '{subject}'.");

    private static AlertConditionDto RequirePositive(AlertConditionDto c) =>
        c.Threshold > 0 ? c : throw new ValidationException("Threshold must be greater than zero.");

    private static AlertConditionDto RequireParam(AlertConditionDto c, int min, int max) =>
        c.Param >= min && c.Param <= max ? c
            : throw new ValidationException($"Period must be between {min} and {max}.");

    private static AlertRuleDto ToDto(AlertRule rule)
    {
        var conditions = JsonSerializer.Deserialize<List<AlertConditionDto>>(rule.ConditionsJson) ?? [];
        return new AlertRuleDto(rule.Id, rule.Name, rule.Enabled, conditions, rule.LastTriggeredAt,
            AlertRuleSummary.Describe(conditions));
    }
}
