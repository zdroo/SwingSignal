using SwingSignal.Contracts.Backtests;

namespace SwingSignal.Application.Backtesting;

public interface IBacktestService
{
    Task<BacktestResultDto> RunAsync(
        string symbol, int days, int topK = 10,
        int? fromYear = null, int? toYear = null, double? stateBandwidth = null,
        CancellationToken ct = default);

    Task<BacktestComparisonDto> CompareAsync(
        string symbol, int days, int topK = 10,
        int? fromYear = null, int? toYear = null,
        CancellationToken ct = default);
}
