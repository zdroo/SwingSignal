using Microsoft.EntityFrameworkCore;
using SwingSignal.Application.Abstractions.Persistence;
using SwingSignal.Domain.Entities;

namespace SwingSignal.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly SwingSignalDbContext _context;

    public UserRepository(SwingSignalDbContext context) => _context = context;

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _context.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        _context.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken ct = default) =>
        _context.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken, ct);

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default) =>
        _context.Users.AnyAsync(u => u.Email == email, ct);

    public Task<User?> GetByEmailConfirmationTokenAsync(string token, CancellationToken ct = default) =>
        _context.Users.FirstOrDefaultAsync(u => u.EmailConfirmationToken == token, ct);

    public Task<User?> GetByPasswordResetTokenAsync(string token, CancellationToken ct = default) =>
        _context.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == token, ct);

    public Task<int> DeleteUnconfirmedOlderThanAsync(DateTime cutoff, CancellationToken ct = default) =>
        _context.Users
            .Where(u => !u.IsEmailConfirmed && u.CreatedAt < cutoff)
            .ExecuteDeleteAsync(ct);

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);
    }

    public Task<List<User>> GetWeeklyReportRecipientsAsync(DateTime notSentSince, CancellationToken ct = default) =>
        _context.Users
            .Where(u => u.Plan == Domain.Enums.UserPlan.Pro
                && u.WeeklyReportEnabled
                && (u.LastWeeklyReportAt == null || u.LastWeeklyReportAt < notSentSince))
            .ToListAsync(ct);

    public Task<List<User>> GetAlertRecipientsAsync(CancellationToken ct = default) =>
        _context.Users
            .Where(u => u.Plan == Domain.Enums.UserPlan.Pro && u.AlertsEnabled)
            .ToListAsync(ct);

    public Task UpdateAsync(User user, CancellationToken ct = default) =>
        _context.SaveChangesAsync(ct);

    public async Task DeleteAsync(User user, CancellationToken ct = default)
    {
        _context.Users.Remove(user);
        await _context.SaveChangesAsync(ct);
    }
}
