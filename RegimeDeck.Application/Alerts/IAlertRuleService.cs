using RegimeDeck.Contracts.Alerts;

namespace RegimeDeck.Application.Alerts;

public interface IAlertRuleService
{
    Task<List<AlertRuleDto>> GetAsync(Guid userId, CancellationToken ct = default);
    Task<AlertRuleDto> CreateAsync(Guid userId, CreateAlertRuleRequest request, CancellationToken ct = default);
    Task<AlertRuleDto> UpdateAsync(Guid userId, Guid id, UpdateAlertRuleRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid userId, Guid id, CancellationToken ct = default);
}
