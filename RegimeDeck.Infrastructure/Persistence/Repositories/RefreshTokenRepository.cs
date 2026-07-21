using Microsoft.EntityFrameworkCore;
using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Infrastructure.Persistence.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly RegimeDeckDbContext _context;

    public RefreshTokenRepository(RegimeDeckDbContext context) => _context = context;

    public async Task AddAsync(RefreshToken token, CancellationToken ct = default)
    {
        _context.RefreshTokens.Add(token);
        await _context.SaveChangesAsync(ct);
    }

    // Tracked (not AsNoTracking): RotateAsync mutates the returned entity.
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default) =>
        string.IsNullOrEmpty(tokenHash)
            ? Task.FromResult<RefreshToken?>(null)
            : _context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task RotateAsync(RefreshToken current, RefreshToken replacement, CancellationToken ct = default)
    {
        current.RevokedAt = replacement.CreatedAt;
        current.ReplacedByTokenId = replacement.Id;
        _context.RefreshTokens.Add(replacement);
        // One SaveChanges: the old token is revoked and the new one stored atomically
        await _context.SaveChangesAsync(ct);
    }

    public Task RevokeFamilyAsync(Guid familyId, DateTime now, CancellationToken ct = default) =>
        _context.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);

    public Task RevokeAllForUserAsync(Guid userId, DateTime now, CancellationToken ct = default) =>
        _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
}
