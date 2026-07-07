namespace SwingSignal.Contracts.Regime;

public record MacroIndicatorValueDto(
    decimal Value,
    string Signal,   // e.g. "Restrictive" | "Neutral" | "Accommodative"
    string Trend);   // "Rising" | "Falling" | "Stable"

public record MacroRegimeDto(
    Dictionary<string, MacroIndicatorValueDto> Indicators,
    DateTime AsOf);

public record HistoricalMatchDto(
    DateTime Date,
    double SimilarityScore,
    double TopPercent,   // this month is closer to today than (100 - TopPercent)% of all history
    Dictionary<string, decimal> IndicatorValues);

public record OddsForPeriodDto(
    int TotalCases,
    int PositiveCases,
    double PositiveOdds,       // shrunk toward the base rate — what we actually claim
    decimal AverageReturn,
    decimal MedianReturn,
    decimal BestCase,
    decimal WorstCase,
    decimal? PriceTargetLow,   // P25 absolute price
    decimal? PriceTargetMid,   // P50 absolute price
    decimal? PriceTargetHigh,  // P75 absolute price
    double? BaseRate = null,   // % of ALL historical windows of this length that were positive
    double Edge = 0);          // PositiveOdds - BaseRate: what the current regime adds

public record AssetOddsDto(
    string Symbol,
    string Name,
    int MatchesUsed,
    decimal? CurrentPrice,
    OddsForPeriodDto OneMonth,
    OddsForPeriodDto ThreeMonths,
    OddsForPeriodDto SixMonths,
    List<string> Explanations,
    string Disclaimer);

// Odds for a single user-selected horizon (7-365 days)
public record AssetPeriodOddsDto(
    string Symbol,
    string Name,
    int Days,
    int MatchesUsed,
    decimal? CurrentPrice,
    OddsForPeriodDto Odds,
    string Disclaimer);
