using System.Collections.Concurrent;

namespace RegimeDeck.Application.Common;

/// Per-user daily usage counter for quota-limited features. In-memory by
/// design: a restart resetting quotas is acceptable for a generosity cap,
/// and it keeps the hot path free of database writes.
public interface IDailyQuota
{
    /// Consumes one unit; false when the user is over today's limit.
    bool TryConsume(Guid userId, int limit);
}

public sealed class InMemoryDailyQuota : IDailyQuota
{
    // One entry per user, overwritten on day change — never grows beyond
    // the active user count
    private readonly ConcurrentDictionary<Guid, (DateOnly Day, int Count)> _usage = new();

    public bool TryConsume(Guid userId, int limit) =>
        TryConsume(userId, limit, DateOnly.FromDateTime(DateTime.UtcNow));

    public bool TryConsume(Guid userId, int limit, DateOnly today)
    {
        var updated = _usage.AddOrUpdate(
            userId,
            _ => (today, 1),
            (_, current) => current.Day == today ? (today, current.Count + 1) : (today, 1));

        return updated.Count <= limit;
    }
}
