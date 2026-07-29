using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Application.Alerts;
using RegimeDeck.Application.Common;
using RegimeDeck.Contracts.Alerts;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Tests.Alerts;

public class AlertRuleServiceTests
{
    private static readonly Guid User = Guid.NewGuid();

    private readonly FakeRepo _repo = new();
    private AlertRuleService Service() => new(_repo);

    private static AlertConditionDto Cond(string type, string subject, string op, double threshold = 0, int param = 0) =>
        new(type, subject, op, threshold, param);

    private static CreateAlertRuleRequest Create(string name, params AlertConditionDto[] conditions) =>
        new(name, [.. conditions]);

    [Fact]
    public async Task Create_NormalizesSubjects_AndSummarizes()
    {
        var dto = await Service().CreateAsync(User, Create("Risk-off watch",
            Cond(AlertConditionTypes.MacroIndicator, "vix", "Above", 30),
            Cond(AlertConditionTypes.MovingAverage, "spy", "Below", param: 200)));

        Assert.Equal("VIX", dto.Conditions[0].Subject);   // canonical macro name
        Assert.Equal("SPY", dto.Conditions[1].Subject);   // normalized symbol
        Assert.Equal("VIX above 30 AND SPY below its 200-day average", dto.Summary);
        Assert.True(dto.Enabled);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_BlankName_Throws(string name)
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            Service().CreateAsync(User, Create(name, Cond(AlertConditionTypes.MacroIndicator, "VIX", "Above", 30))));
    }

    [Fact]
    public async Task Create_NoConditions_Throws()
    {
        await Assert.ThrowsAsync<ValidationException>(() => Service().CreateAsync(User, Create("x")));
    }

    [Fact]
    public async Task Create_TooManyConditions_Throws()
    {
        var many = Enumerable.Range(0, 6).Select(_ => Cond(AlertConditionTypes.MacroIndicator, "VIX", "Above", 30)).ToArray();
        await Assert.ThrowsAsync<ValidationException>(() => Service().CreateAsync(User, Create("x", many)));
    }

    [Fact]
    public async Task Create_UnknownIndicator_Throws()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            Service().CreateAsync(User, Create("x", Cond(AlertConditionTypes.MacroIndicator, "NOPE", "Above", 1))));
    }

    [Fact]
    public async Task Create_MovingAverageBadPeriod_Throws()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            Service().CreateAsync(User, Create("x", Cond(AlertConditionTypes.MovingAverage, "SPY", "Below", param: 1))));
    }

    [Fact]
    public async Task Create_AtLimit_Throws()
    {
        for (var i = 0; i < AlertRuleService.MaxRules; i++)
            _repo.Store.Add(new AlertRule { UserId = User });

        await Assert.ThrowsAsync<ValidationException>(() =>
            Service().CreateAsync(User, Create("x", Cond(AlertConditionTypes.MacroIndicator, "VIX", "Above", 30))));
    }

    [Fact]
    public async Task Update_TogglesEnabled_AndOwnsRule()
    {
        var created = await Service().CreateAsync(User, Create("x", Cond(AlertConditionTypes.MacroIndicator, "VIX", "Above", 30)));

        var updated = await Service().UpdateAsync(User, created.Id, new UpdateAlertRuleRequest(null, false, null));
        Assert.False(updated.Enabled);
    }

    [Fact]
    public async Task Update_WrongUser_ThrowsNotFound()
    {
        var created = await Service().CreateAsync(User, Create("x", Cond(AlertConditionTypes.MacroIndicator, "VIX", "Above", 30)));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Service().UpdateAsync(Guid.NewGuid(), created.Id, new UpdateAlertRuleRequest("hacked", null, null)));
    }

    [Fact]
    public async Task Delete_WrongUser_ThrowsNotFound()
    {
        var created = await Service().CreateAsync(User, Create("x", Cond(AlertConditionTypes.MacroIndicator, "VIX", "Above", 30)));

        await Assert.ThrowsAsync<NotFoundException>(() => Service().DeleteAsync(Guid.NewGuid(), created.Id));
        Assert.Single(_repo.Store);
    }

    private sealed class FakeRepo : IAlertRuleRepository
    {
        public List<AlertRule> Store { get; } = [];

        public Task<List<AlertRule>> GetByUserAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Store.Where(r => r.UserId == userId).ToList());
        public Task<AlertRule?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Store.FirstOrDefault(r => r.Id == id));
        public Task<int> CountByUserAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Store.Count(r => r.UserId == userId));
        public Task AddAsync(AlertRule rule, CancellationToken ct = default) { Store.Add(rule); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(AlertRule rule, CancellationToken ct = default) { Store.Remove(rule); return Task.CompletedTask; }
        public Task<List<AlertRuleWithOwner>> GetEnabledWithOwnerAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
