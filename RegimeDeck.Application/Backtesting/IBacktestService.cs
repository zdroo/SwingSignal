using RegimeDeck.Contracts.Backtests;

namespace RegimeDeck.Application.Backtesting;

public interface IBacktestService
{
    /// Walk-forward backtest. See BacktestQuery for the standard vs research inputs.
    Task<BacktestResultDto> RunAsync(string symbol, BacktestQuery query, CancellationToken ct = default);

    /// Runs the backtest under both the naive baseline and the current algorithm
    /// so improvements can be measured rather than assumed.
    Task<BacktestComparisonDto> CompareAsync(
        string symbol, int days, int topK = 10, CancellationToken ct = default);
}
