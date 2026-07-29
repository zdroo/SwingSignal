using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Application.Abstractions.Persistence;

public record AlertRuleWithOwner(AlertRule Rule, string Email);

public interface IAlertRuleRepository
{
    Task<List<AlertRule>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<AlertRule?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<int> CountByUserAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(AlertRule rule, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
    Task DeleteAsync(AlertRule rule, CancellationToken ct = default);

    /// Enabled rules joined with their owner's email — for the background evaluator.
    Task<List<AlertRuleWithOwner>> GetEnabledWithOwnerAsync(CancellationToken ct = default);
}
