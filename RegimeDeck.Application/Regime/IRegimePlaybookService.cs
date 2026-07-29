using RegimeDeck.Contracts.Regime;

namespace RegimeDeck.Application.Regime;

public interface IRegimePlaybookService
{
    // Every canonical regime with its asset-class playbook, plus which one
    // today's live readings sit closest to.
    Task<RegimePlaybookBoardDto> GetBoardAsync(CancellationToken ct = default);
}
