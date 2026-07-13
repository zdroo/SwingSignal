using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Application.Abstractions.Ingestion;

/// Registers an unknown symbol and pulls its price history on demand.
/// Implemented in Infrastructure (talks to exchange/market data providers).
public interface IAssetIngestionService
{
    Task<Asset?> EnsureIngestedAsync(string symbol, CancellationToken ct = default);
}
