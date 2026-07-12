using RegimeDeck.Contracts.Regime;

namespace RegimeDeck.Application.Regime;

public interface IMacroRegimeService
{
    Task<MacroRegimeDto> GetCurrentRegimeAsync(CancellationToken ct = default);

    // options selects the matching profile (e.g. crypto dimension subset);
    // minAnalogDate restricts analogs to months the asset was tradable.
    Task<List<HistoricalMatchDto>> FindSimilarPeriodsAsync(
        int topK = 10,
        MatchingOptions? options = null,
        DateTime? minAnalogDate = null,
        CancellationToken ct = default);
}
