using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Application.Abstractions.Persistence;

public interface IRefreshTokenRepository
{
    /// Persists a brand-new token (a fresh login opens a new family).
    Task AddAsync(RefreshToken token, CancellationToken ct = default);

    /// Looks a token up by its hash. Null for a missing/empty hash.
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);

    /// Atomically revokes `current` (pointing it at `replacement`) and stores
    /// `replacement` — the rotation that happens on every successful refresh.
    Task RotateAsync(RefreshToken current, RefreshToken replacement, CancellationToken ct = default);

    /// Revokes every still-active token in one rotation chain — used on logout
    /// and on reuse detection.
    Task RevokeFamilyAsync(Guid familyId, DateTime now, CancellationToken ct = default);

    /// Revokes every still-active token for a user — "sign out everywhere",
    /// triggered by a password change/reset.
    Task RevokeAllForUserAsync(Guid userId, DateTime now, CancellationToken ct = default);
}
