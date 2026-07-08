namespace SwingSignal.Application.Common;

/// Application-level failures with a well-defined HTTP meaning. Services throw
/// these; the API's exception middleware maps each to its status code —
/// controllers never translate exceptions themselves.
public abstract class AppException : Exception
{
    protected AppException(string message) : base(message) { }
}

/// 400 — the request was understood but its content is invalid.
public sealed class ValidationException : AppException
{
    public ValidationException(string message) : base(message) { }
}

/// 401 — credentials or tokens that failed verification.
public sealed class AuthenticationFailedException : AppException
{
    public AuthenticationFailedException(string message) : base(message) { }
}

/// 404 — the addressed resource does not exist.
public sealed class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message) { }
}

/// 409 — the request conflicts with existing state.
public sealed class ConflictException : AppException
{
    public ConflictException(string message) : base(message) { }
}

/// 429 — a per-account throttle (distinct from the IP rate limiter).
public sealed class RateLimitedException : AppException
{
    public RateLimitedException(string message) : base(message) { }
}
