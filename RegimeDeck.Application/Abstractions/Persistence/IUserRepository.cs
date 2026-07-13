using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Application.Abstractions.Persistence;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task<User?> GetByEmailConfirmationTokenAsync(string token, CancellationToken ct = default);
    Task<User?> GetByPasswordResetTokenAsync(string token, CancellationToken ct = default);
    Task<int> DeleteUnconfirmedOlderThanAsync(DateTime cutoff, CancellationToken ct = default);
    /// Pro users with the weekly report enabled who haven't received one since the cutoff.
    Task<List<User>> GetWeeklyReportRecipientsAsync(DateTime notSentSince, CancellationToken ct = default);
    /// Pro users with change alerts enabled.
    Task<List<User>> GetAlertRecipientsAsync(CancellationToken ct = default);
    Task<User?> GetByStripeSubscriptionIdAsync(string subscriptionId, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
    Task DeleteAsync(User user, CancellationToken ct = default);
}
