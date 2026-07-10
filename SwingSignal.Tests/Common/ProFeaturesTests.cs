using SwingSignal.Application.Common;

namespace SwingSignal.Tests.Common;

// The dark-launch semantics in one truth table: a gate only ever bites when
// the flag is on AND the caller isn't Pro.
public class ProFeaturesTests
{
    [Theory]
    [InlineData(false, false, false)] // flag off → free users unaffected
    [InlineData(false, true, false)]  // flag off → Pro users unaffected
    [InlineData(true, false, true)]   // flag on  → free users gated
    [InlineData(true, true, false)]   // flag on  → Pro users pass
    public void GateActive_TruthTable(bool enabled, bool isPro, bool expected)
    {
        Assert.Equal(expected, new ProFeatures(enabled).GateActive(isPro));
    }

    [Fact]
    public void RequirePro_Throws403OnlyWhenGateActive()
    {
        var ex = Assert.Throws<ForbiddenException>(
            () => new ProFeatures(enabled: true).RequirePro(isPro: false, "Nope."));
        Assert.Equal("Nope.", ex.Message);

        new ProFeatures(enabled: true).RequirePro(isPro: true, "unused");
        new ProFeatures(enabled: false).RequirePro(isPro: false, "unused");
    }
}

public class InMemoryDailyQuotaTests
{
    private static readonly DateOnly Today = new(2026, 7, 11);

    [Fact]
    public void ConsumesUpToLimit_ThenRefuses()
    {
        var quota = new InMemoryDailyQuota();
        var user = Guid.NewGuid();

        for (var i = 0; i < 3; i++)
            Assert.True(quota.TryConsume(user, limit: 3, Today));

        Assert.False(quota.TryConsume(user, limit: 3, Today));
    }

    [Fact]
    public void NewDay_ResetsTheCount()
    {
        var quota = new InMemoryDailyQuota();
        var user = Guid.NewGuid();

        Assert.True(quota.TryConsume(user, limit: 1, Today));
        Assert.False(quota.TryConsume(user, limit: 1, Today));

        Assert.True(quota.TryConsume(user, limit: 1, Today.AddDays(1)));
    }

    [Fact]
    public void UsersAreCountedIndependently()
    {
        var quota = new InMemoryDailyQuota();

        Assert.True(quota.TryConsume(Guid.NewGuid(), limit: 1, Today));
        Assert.True(quota.TryConsume(Guid.NewGuid(), limit: 1, Today));
    }
}
