using Microsoft.EntityFrameworkCore;
using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly RegimeDeckDbContext _context;

    public UserRepository(RegimeDeckDbContext context) => _context = context;

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _context.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        _context.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    // A null/empty token must NEVER match a row. EF Core uses C# null semantics,
    // so `col == null` compiles to `col IS NULL` — without this guard a request
    // carrying no token would match the first user whose token column is null
    // (i.e. anyone without a pending reset/confirmation) and hand back their
    // account. Short-circuit before the query is ever built.
    public Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken ct = default) =>
        string.IsNullOrEmpty(refreshToken)
            ? Task.FromResult<User?>(null)
            : _context.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken, ct);

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default) =>
        _context.Users.AnyAsync(u => u.Email == email, ct);

    public Task<User?> GetByEmailConfirmationTokenAsync(string token, CancellationToken ct = default) =>
        string.IsNullOrEmpty(token)
            ? Task.FromResult<User?>(null)
            : _context.Users.FirstOrDefaultAsync(u => u.EmailConfirmationToken == token, ct);

    public Task<User?> GetByPasswordResetTokenAsync(string token, CancellationToken ct = default) =>
        string.IsNullOrEmpty(token)
            ? Task.FromResult<User?>(null)
            : _context.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == token, ct);

    public Task<int> DeleteUnconfirmedOlderThanAsync(DateTime cutoff, CancellationToken ct = default) =>
        _context.Users
            .Where(u => !u.IsEmailConfirmed && u.CreatedAt < cutoff)
            .ExecuteDeleteAsync(ct);

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);
    }

    // IsEmailConfirmed: never send content to an address the owner hasn't verified
    public Task<List<User>> GetWeeklyReportRecipientsAsync(DateTime notSentSince, CancellationToken ct = default) =>
        _context.Users
            .Where(u => u.Plan == Domain.Enums.UserPlan.Pro
                && u.WeeklyReportEnabled
                && u.IsEmailConfirmed
                && (u.LastWeeklyReportAt == null || u.LastWeeklyReportAt < notSentSince))
            .ToListAsync(ct);

    public Task<List<User>> GetAlertRecipientsAsync(CancellationToken ct = default) =>
        _context.Users
            .Where(u => u.Plan == Domain.Enums.UserPlan.Pro && u.AlertsEnabled && u.IsEmailConfirmed)
            .ToListAsync(ct);

    public Task<User?> GetByStripeSubscriptionIdAsync(string subscriptionId, CancellationToken ct = default) =>
        _context.Users.FirstOrDefaultAsync(u => u.StripeSubscriptionId == subscriptionId, ct);

    public Task UpdateAsync(User user, CancellationToken ct = default) =>
        _context.SaveChangesAsync(ct);

    public async Task DeleteAsync(User user, CancellationToken ct = default)
    {
        _context.Users.Remove(user);
        await _context.SaveChangesAsync(ct);
    }
}
