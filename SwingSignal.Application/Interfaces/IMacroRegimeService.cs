using SwingSignal.Application.DTOs;

namespace SwingSignal.Application.Interfaces;

public interface IMacroRegimeService
{
    Task<MacroRegimeDto> GetCurrentRegimeAsync(CancellationToken ct = default);
    Task<List<HistoricalMatchDto>> FindSimilarPeriodsAsync(int topK = 10, CancellationToken ct = default);
}
