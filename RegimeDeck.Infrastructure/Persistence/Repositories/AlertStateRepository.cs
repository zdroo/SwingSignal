using Microsoft.EntityFrameworkCore;
using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Infrastructure.Persistence.Repositories;

public class AlertStateRepository : IAlertStateRepository
{
    private readonly RegimeDeckDbContext _context;

    public AlertStateRepository(RegimeDeckDbContext context) => _context = context;

    public Task<List<AlertState>> GetAllAsync(CancellationToken ct = default) =>
        _context.AlertStates
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task UpsertAsync(string key, string value, CancellationToken ct = default)
    {
        var existing = await _context.AlertStates.FirstOrDefaultAsync(a => a.Key == key, ct);
        if (existing is null)
        {
            _context.AlertStates.Add(new AlertState { Key = key, Value = value, UpdatedAt = DateTime.UtcNow });
        }
        else
        {
            existing.Value = value;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync(ct);
    }
}
