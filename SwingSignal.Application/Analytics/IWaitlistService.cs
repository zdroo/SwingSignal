namespace SwingSignal.Application.Analytics;

public interface IWaitlistService
{
    /// Throws ValidationException for an invalid email. Idempotent: an
    /// already-registered email is treated as success.
    Task JoinAsync(string email, string? source, Guid? userId, CancellationToken ct = default);
}
