using SwingSignal.Domain.Entities;

namespace SwingSignal.Application.Abstractions.Persistence;

public interface IAlertStateRepository
{
    Task<List<AlertState>> GetAllAsync(CancellationToken ct = default);
    Task UpsertAsync(string key, string value, CancellationToken ct = default);
}
