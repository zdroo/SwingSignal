using SwingSignal.Contracts.Assets;

namespace SwingSignal.Application.Abstractions.Ingestion;

/// Free-text symbol lookup ("apple" -> AAPL) backed by a market data provider.
public interface ISymbolSearchService
{
    Task<List<SymbolSearchResultDto>> SearchAsync(string query, CancellationToken ct = default);
}
