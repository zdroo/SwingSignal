using SwingSignal.Contracts.Regime;

namespace SwingSignal.Application.Regime;

public interface IMacroRegimeService
{
    Task<MacroRegimeDto> GetCurrentRegimeAsync(CancellationToken ct = default);
    Task<List<HistoricalMatchDto>> FindSimilarPeriodsAsync(int topK = 10, CancellationToken ct = default);
}
