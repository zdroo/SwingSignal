namespace RegimeDeck.Contracts.Regime;

public record MacroIndicatorValueDto(
    decimal Value,
    string Signal,        // e.g. "Restrictive" | "Neutral" | "Accommodative"
    string Trend,         // "Rising" | "Falling" | "Stable"
    string Tone,          // "good" | "neutral" | "caution" | "bad"
    int Severity,         // 0 background … 3 extreme
    bool IsMarketMover);  // severity at or above the market-mover threshold

public record HealthMemberDto(string Key, string Signal, string Tone);

public record HealthGroupDto(
    string Name,
    string Question,
    int Score,            // 0–100
    string Label,         // Stressed | Strained | Mixed | Steady | Supportive
    List<HealthMemberDto> Members);

/// Descriptive composite of today's signal states — summarizes the
/// dashboard, is NOT a prediction and plays no role in the odds engine.
public record MarketHealthDto(int Score, string Label, List<HealthGroupDto> Groups);

public record PlaybookAssetDto(
    string Name,
    int Score,            // 0–100 playbook fit for new money
    string Verdict,       // Favored | Neutral | Headwinds
    List<string> Reasons);

/// Which asset classes current conditions favor for NEW money — a fixed,
/// textbook playbook applied to today's readings. Descriptive, not advice,
/// and separate from the odds engine.
public record PlaybookDto(string Headline, string Note, List<PlaybookAssetDto> Assets);

public record MacroRegimeDto(
    Dictionary<string, MacroIndicatorValueDto> Indicators,
    MarketHealthDto Health,
    List<string> Summary, // the rule-based plain-words narrative
    PlaybookDto Playbook,
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

// One analog month with the asset's own price state at that time.
// AboveMa200 is null when the asset didn't have 200 days of history yet.
public record AnalogPointDto(DateTime Date, bool? AboveMa200);

// The analogs split by the asset's price state — descriptive context, not a
// validated predictor (state-conditioning failed our out-of-sample tests).
public record AnalogBreakdownDto(
    bool? CurrentAboveMa200,
    int AboveCount,
    double? AboveOdds3M,       // % of above-MA analogs with a positive 3M return
    decimal? AboveMedian3M,    // median 3M return of that group
    int BelowCount,
    double? BelowOdds3M,
    decimal? BelowMedian3M,
    List<AnalogPointDto> Points);

/// What the analog statistics support right now for this asset — a fixed
/// rule-based read of the odds (edge vs base rate, consistency across
/// horizons, sample size, risk spread). Descriptive, not advice.
public record TradeReadDto(
    string Stance,       // "Long bias" | "No edge" | "Stand aside"
    int HorizonDays,     // the horizon the read keys on: 30 | 90 | 180
    string Strength,     // "Strong" | "Moderate" | "Weak"
    List<string> Reasons,
    string Note);

public record AssetOddsDto(
    string Symbol,
    string Name,
    int MatchesUsed,
    decimal? CurrentPrice,
    OddsForPeriodDto OneMonth,
    OddsForPeriodDto ThreeMonths,
    OddsForPeriodDto SixMonths,
    List<string> Explanations,
    string Disclaimer,
    AnalogBreakdownDto? Breakdown = null,
    TradeReadDto? TradeRead = null);

// Odds for a single user-selected horizon (7-365 days)
public record AssetPeriodOddsDto(
    string Symbol,
    string Name,
    int Days,
    int MatchesUsed,
    decimal? CurrentPrice,
    OddsForPeriodDto Odds,
    string Disclaimer);
