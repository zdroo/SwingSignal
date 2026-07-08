using System.Security.Claims;

namespace SwingSignal.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// The authenticated user's id, or null for anonymous/malformed tokens.
    public static Guid? GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    /// For [Authorize] endpoints where a valid user id is guaranteed by the middleware.
    public static Guid RequireUserId(this ClaimsPrincipal user) =>
        user.GetUserId() ?? throw new UnauthorizedAccessException("Invalid token.");
}
