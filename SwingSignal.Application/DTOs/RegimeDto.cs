namespace SwingSignal.Application.DTOs;

public record MacroIndicatorValueDto(
    decimal Value,
    string Signal,   // "Restrictive" | "Neutral" | "Accommodative"
    string Trend);   // "Rising" | "Falling" | "Stable"

public record MacroRegimeDto(
    Dictionary<string, MacroIndicatorValueDto> Indicators,
    DateTime AsOf);

public record HistoricalMatchDto(
    DateTime Date,
    double SimilarityScore,
    Dictionary<string, decimal> IndicatorValues);

public record OddsForPeriodDto(
    int TotalCases,
    int PositiveCases,
    double PositiveOdds,
    decimal AverageReturn,
    decimal MedianReturn,
    decimal BestCase,
    decimal WorstCase);

public record AssetOddsDto(
    string Symbol,
    int MatchesUsed,
    OddsForPeriodDto OneMonth,
    OddsForPeriodDto ThreeMonths,
    OddsForPeriodDto SixMonths,
    string Disclaimer);
