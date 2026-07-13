namespace RegimeDeck.Application.Analytics;

public interface ISearchLogService
{
    /// Best-effort demand logging — never throws, a failed log must not fail the request.
    Task LogAsync(string symbol, string? rawQuery, string? source, Guid? userId, bool wasGated, CancellationToken ct = default);
}
