using SwingSignal.Application.Abstractions.Persistence;
using SwingSignal.Application.Common;
using SwingSignal.Domain.Entities;

namespace SwingSignal.Application.Analytics;

public class WaitlistService : IWaitlistService
{
    private const int MaxEmailLength = 256;
    private const int MaxSourceLength = 64;

    private readonly IAnalyticsRepository _analytics;

    public WaitlistService(IAnalyticsRepository analytics) => _analytics = analytics;

    public async Task JoinAsync(string email, string? source, Guid? userId, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalized) || !normalized.Contains('@') || normalized.Length > MaxEmailLength)
            throw new ValidationException("A valid email address is required.");

        await _analytics.AddToWaitlistAsync(new WaitlistEntry
        {
            Email = normalized,
            UserId = userId,
            Source = source?[..Math.Min(source.Length, MaxSourceLength)],
            CreatedAt = DateTime.UtcNow
        }, ct);
    }
}
