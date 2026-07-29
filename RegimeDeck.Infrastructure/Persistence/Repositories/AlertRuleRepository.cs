using Microsoft.EntityFrameworkCore;
using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Infrastructure.Persistence.Repositories;

public class AlertRuleRepository : IAlertRuleRepository
{
    private readonly RegimeDeckDbContext _context;

    public AlertRuleRepository(RegimeDeckDbContext context) => _context = context;

    public Task<List<AlertRule>> GetByUserAsync(Guid userId, CancellationToken ct = default) =>
        _context.AlertRules
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

    public Task<AlertRule?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _context.AlertRules.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<int> CountByUserAsync(Guid userId, CancellationToken ct = default) =>
        _context.AlertRules.CountAsync(r => r.UserId == userId, ct);

    public async Task AddAsync(AlertRule rule, CancellationToken ct = default)
    {
        _context.AlertRules.Add(rule);
        await _context.SaveChangesAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);

    public async Task DeleteAsync(AlertRule rule, CancellationToken ct = default)
    {
        _context.AlertRules.Remove(rule);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<AlertRuleWithOwner>> GetEnabledWithOwnerAsync(CancellationToken ct = default)
    {
        var rows = await _context.AlertRules
            .Where(r => r.Enabled)
            .Join(_context.Users, r => r.UserId, u => u.Id, (r, u) => new { Rule = r, u.Email })
            .ToListAsync(ct);

        return [.. rows.Select(x => new AlertRuleWithOwner(x.Rule, x.Email))];
    }
}
