using Microsoft.EntityFrameworkCore;
using SwingSignal.Application.Abstractions.Persistence;
using SwingSignal.Domain.Entities;

namespace SwingSignal.Infrastructure.Persistence.Repositories;

public class AlertStateRepository : IAlertStateRepository
{
    private readonly SwingSignalDbContext _context;

    public AlertStateRepository(SwingSignalDbContext context) => _context = context;

    public Task<Dictionary<string, string>> GetAllAsync(CancellationToken ct = default) =>
        _context.AlertStates
            .AsNoTracking()
            .ToDictionaryAsync(a => a.Key, a => a.Value, ct);

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
