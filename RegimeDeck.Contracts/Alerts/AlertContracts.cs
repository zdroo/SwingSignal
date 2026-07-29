namespace RegimeDeck.Contracts.Alerts;

/// One condition in an alert rule.
/// Type: "MacroIndicator" | "AssetPrice" | "MovingAverage" | "VolumeSpike".
/// Operator: "Above" | "Below".
/// Threshold meaning by type — MacroIndicator/AssetPrice: the value; VolumeSpike:
/// the multiple of the N-day average. MovingAverage ignores it. Param: the MA
/// length (MovingAverage) or volume lookback (VolumeSpike).
public record AlertConditionDto(
    string Type,
    string Subject,
    string Operator,
    double Threshold,
    int Param);

public record AlertRuleDto(
    Guid Id,
    string Name,
    bool Enabled,
    List<AlertConditionDto> Conditions,
    DateTime? LastTriggeredAt,
    string Summary);

public record CreateAlertRuleRequest(string Name, List<AlertConditionDto> Conditions);

public record UpdateAlertRuleRequest(string? Name, bool? Enabled, List<AlertConditionDto>? Conditions);
