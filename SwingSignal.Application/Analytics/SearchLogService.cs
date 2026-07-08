using Microsoft.Extensions.Logging;
using SwingSignal.Application.Abstractions.Persistence;
using SwingSignal.Domain.Entities;

namespace SwingSignal.Application.Analytics;

public class SearchLogService : ISearchLogService
{
    private const int MaxQueryLength = 200;

    private readonly IAnalyticsRepository _analytics;
    private readonly ILogger<SearchLogService> _logger;

    public SearchLogService(IAnalyticsRepository analytics, ILogger<SearchLogService> logger)
    {
        _analytics = analytics;
        _logger = logger;
    }

    public async Task LogAsync(
        string symbol, string? rawQuery, string? source, Guid? userId, bool wasGated,
        CancellationToken ct = default)
    {
        try
        {
            await _analytics.LogSearchAsync(new SearchLog
            {
                Symbol = symbol,
                RawQuery = string.IsNullOrWhiteSpace(rawQuery) ? null : rawQuery[..Math.Min(rawQuery.Length, MaxQueryLength)],
                Source = string.IsNullOrWhiteSpace(source) ? "direct" : source,
                UserId = userId,
                WasGated = wasGated,
                CreatedAt = DateTime.UtcNow
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Search logging failed for {Symbol}", symbol);
        }
    }
}
