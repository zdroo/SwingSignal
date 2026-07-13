using RegimeDeck.Contracts.Screener;

namespace RegimeDeck.Application.Screener;

public interface IScreenerService
{
    /// The screener board. Free callers get the teaser subset (query ignored);
    /// Pro callers get the full universe with optional filters applied.
    Task<ScreenerResultDto> GetAsync(bool isPro, ScreenerQuery? query, CancellationToken ct = default);
}
