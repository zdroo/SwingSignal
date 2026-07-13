namespace RegimeDeck.Contracts.Backtests;

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
