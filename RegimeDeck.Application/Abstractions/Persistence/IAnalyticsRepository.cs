using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Application.Abstractions.Persistence;

public interface IAnalyticsRepository
{
    Task LogSearchAsync(SearchLog entry, CancellationToken ct = default);

    /// Distinct-symbol view counts within the window, most viewed first.
    Task<List<(string Symbol, int Views)>> GetTopSymbolsAsync(int days, int count, CancellationToken ct = default);

    /// Adds an email to the Pro waitlist. Returns false when already present (idempotent).
    Task<bool> AddToWaitlistAsync(WaitlistEntry entry, CancellationToken ct = default);

    /// GDPR: unlinks a deleted user's search history (rows stay, user reference goes).
    Task DetachUserAsync(Guid userId, CancellationToken ct = default);
}
