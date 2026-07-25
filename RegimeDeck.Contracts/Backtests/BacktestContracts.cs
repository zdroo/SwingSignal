namespace RegimeDeck.Contracts.Backtests;

/// The inputs to a backtest run. The bare (Days, TopK) call is the free standard
/// backtest; the rest are Pro "research knobs" for A/B-validating the matching
/// algorithm. Short query names are the public API; the model owns its own
/// validation so the controller stays thin.
public record BacktestQuery(
    int Days = 90,
    int TopK = 10,
    int? FromYear = null,             // restrict evaluation to years >= FromYear
    int? ToYear = null,               // restrict evaluation to years < ToYear
    double? StateH = null,            // asset-state conditioning bandwidth (0 disables it)
    string? Profile = null,           // "crypto" | "crypto-native" | "default"; null = auto by market
    bool? FloorHistory = null,        // floor analog candidates to the asset's first candle
    double? CycleH = null,            // crypto-cycle conditioning bandwidth (0 disables it)
    double? ShrinkM = null,           // adaptive-shrinkage prior sample size (0 disables it)
    int? BaseRateYears = null)        // trailing-years base-rate window (0 = all history)
{
    /// Anything beyond the standard (Days, TopK) run is a Pro research knob.
    public bool HasResearchParams =>
        FromYear is not null || ToYear is not null || StateH is not null || Profile is not null
        || FloorHistory is not null || CycleH is not null || ShrinkM is not null || BaseRateYears is not null;

    /// Null when valid, otherwise a human-readable reason. Guards the ranges and,
    /// crucially, the model binder's "NaN"/"Infinity" parses and unbounded values
    /// that would otherwise crash the engine (invalid-JSON NaN, AddYears overflow).
    public string? Validate()
    {
        if (Days is < 7 or > 365) return "days must be between 7 and 365";
        if (TopK is < 1 or > 20) return "topK must be between 1 and 20";
        if (Profile is not null and not "crypto" and not "default" and not "crypto-native")
            return "profile must be 'crypto', 'crypto-native' or 'default'";
        if (BadDouble(StateH)) return "stateH must be a finite value >= 0";
        if (BadDouble(CycleH)) return "cycleH must be a finite value >= 0";
        if (BadDouble(ShrinkM)) return "shrinkM must be a finite value >= 0";
        if (BaseRateYears is < 0 or > 200) return "baseRateYears must be between 0 and 200";
        if (FromYear is < 1900 or > 2100) return "fromYear must be between 1900 and 2100";
        if (ToYear is < 1900 or > 2100) return "toYear must be between 1900 and 2100";
        if (FromYear is int fy && ToYear is int ty && fy > ty) return "fromYear must not be after toYear";
        return null;
    }

    private static bool BadDouble(double? value) => value is double d && (!double.IsFinite(d) || d < 0);
}

// One bucket of the calibration report: e.g. "when we predicted 60-70%, what actually happened"
public record BacktestBucketDto(
    string PredictedRange,
    int Predictions,
    double AvgPredictedOdds,
    double ActualPositiveRate);

public record BacktestResultDto(
    string Symbol,
    string Name,
    int Days,
    int TopK,
    int TotalPredictions,
    DateTime FirstPrediction,
    DateTime LastPrediction,
    double DirectionalAccuracy,   // % of months where predicted direction matched actual
    double BrierScore,            // 0 = perfect, 0.25 = coin flip
    double AvgPredictedOdds,
    double ActualPositiveRate,
    List<BacktestBucketDto> Calibration,
    string Interpretation,
    string Disclaimer);

// Same backtest run under the naive baseline algorithm and the current one,
// so improvements can be measured instead of assumed.
public record BacktestComparisonDto(
    BacktestResultDto Baseline,
    BacktestResultDto Current,
    string Summary);
