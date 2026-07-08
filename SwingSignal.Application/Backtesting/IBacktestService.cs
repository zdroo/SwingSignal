using SwingSignal.Contracts.Backtests;

namespace SwingSignal.Application.Backtesting;

public interface IBacktestService
{
    // profile: null = auto by market type (production behavior), "crypto" or
    // "default" force a matching profile. floorHistory: null = follows the
    // profile; true/false force analog candidates to start at the asset's
    // first candle or not. Both exist so configurations can be A/B validated.
    Task<BacktestResultDto> RunAsync(
        string symbol, int days, int topK = 10,
        int? fromYear = null, int? toYear = null, double? stateBandwidth = null,
        string? profile = null, bool? floorHistory = null,
        CancellationToken ct = default);

    Task<BacktestComparisonDto> CompareAsync(
        string symbol, int days, int topK = 10,
        int? fromYear = null, int? toYear = null,
        CancellationToken ct = default);
}
