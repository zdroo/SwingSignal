using System.Net.Mime;
using System.Text.Json;
using SwingSignal.Application.Common;

namespace SwingSignal.Api.Middleware;

/// The single place exceptions become HTTP responses: services throw
/// AppException subtypes with a well-defined status, controllers stay free of
/// try/catch, and no stack trace ever leaks to a client. The BCL mappings
/// below are a safety net for framework- and library-thrown exceptions.
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var (status, message) = ex switch
            {
                ValidationException => (StatusCodes.Status400BadRequest, ex.Message),
                AuthenticationFailedException => (StatusCodes.Status401Unauthorized, ex.Message),
                ForbiddenException => (StatusCodes.Status403Forbidden, ex.Message),
                NotFoundException => (StatusCodes.Status404NotFound, ex.Message),
                ConflictException => (StatusCodes.Status409Conflict, ex.Message),
                RateLimitedException => (StatusCodes.Status429TooManyRequests, ex.Message),

                ArgumentException or ArgumentOutOfRangeException => (StatusCodes.Status400BadRequest, ex.Message),
                InvalidOperationException => (StatusCodes.Status400BadRequest, ex.Message),
                UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, ex.Message),
                KeyNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
                OperationCanceledException => (499, "Request was cancelled."),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred."),
            };

            if (status == StatusCodes.Status500InternalServerError)
                _logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
            else
                _logger.LogWarning("Handled {ExceptionType} on {Method} {Path}: {Message}",
                    ex.GetType().Name, context.Request.Method, context.Request.Path, ex.Message);

            if (context.Response.HasStarted) throw;

            context.Response.StatusCode = status;
            context.Response.ContentType = MediaTypeNames.Application.Json;
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { message }));
        }
    }
}
