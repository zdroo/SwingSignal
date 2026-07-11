namespace SwingSignal.Application.Abstractions.Persistence;

public interface IAlertStateRepository
{
    Task<Dictionary<string, string>> GetAllAsync(CancellationToken ct = default);
    Task UpsertAsync(string key, string value, CancellationToken ct = default);
}
