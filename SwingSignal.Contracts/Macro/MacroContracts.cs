namespace SwingSignal.Contracts.Macro;

public record MacroDataPointDto(string IndicatorType, DateTime Date, decimal Value, string Source);

public record MacroSnapshotDto(Dictionary<string, decimal?> Indicators, DateTime AsOf);
