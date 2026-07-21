namespace RegimeDeck.Api.Auth;

/// The single place the refresh-token cookie's name and attributes live, shared
/// by AuthController (login/refresh/logout) and UsersController (change-password,
/// which re-issues the current session). HttpOnly so JavaScript can never read it.
public static class RefreshTokenCookie
{
    public const string Name = "rd_refresh";
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(365);

    public static string? Read(HttpRequest request) => request.Cookies[Name];

    public static void Set(HttpResponse response, string token) =>
        response.Cookies.Append(Name, token,
            Options(response.HttpContext.Request, DateTimeOffset.UtcNow.Add(Lifetime)));

    public static void Clear(HttpResponse response) =>
        // Same attributes, already expired — the reliable way to make the browser drop it
        response.Cookies.Append(Name, "", Options(response.HttpContext.Request, DateTimeOffset.UnixEpoch));

    private static CookieOptions Options(HttpRequest request, DateTimeOffset expires) => new()
    {
        HttpOnly = true,                                        // JS can never read it
        Secure = request.IsHttps,                               // HTTPS-only in prod; off for http://localhost
        SameSite = request.IsHttps ? SameSiteMode.None : SameSiteMode.Lax, // None needed for cross-site prod
        Path = "/api/auth",                                     // only sent to the auth endpoints
        Expires = expires,
        IsEssential = true,
    };
}
