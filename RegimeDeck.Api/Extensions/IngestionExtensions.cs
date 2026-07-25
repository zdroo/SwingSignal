using RegimeDeck.Application.Abstractions.Ingestion;
using RegimeDeck.Application.Common;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Api.Extensions;

public static class IngestionExtensions
{
    /// Normalizes the symbol, ensures its market data is ingested, and returns the
    /// asset — throwing a 503-mapped ServiceUnavailableException when the symbol
    /// can't be fetched (a typo/unsupported ticker or a provider outage).
    /// `asset.Symbol` is the normalized form, so callers use it downstream.
    public static async Task<Asset> EnsureSupportedAsync(
        this IAssetIngestionService ingestion, string symbol, CancellationToken ct)
    {
        var normalized = SymbolNormalizer.Normalize(symbol);
        return await ingestion.EnsureIngestedAsync(normalized, ct)
            ?? throw new ServiceUnavailableException(
                $"Could not fetch data for symbol '{normalized}'. The symbol may not be supported.");
    }
}
