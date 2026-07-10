using System.Net;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.RateLimiting;

namespace SwingSignal.Api.Extensions;

/// IP-partitioned rate limiting. Policies:
///   global            300/min  — blanket protection for everything
///   auth               10/min  — login/refresh/token endpoints (brute-force)
///   register            5/hour — account creation (abusive signups)
///   public-sensitive   20/min  — endpoints that proxy third parties (Yahoo search)
///   compute            10/min  — expensive analysis (backtests, custom windows)
///   odds               30/min  — asset odds; can trigger external ingestion for
///                                new symbols. Invisible to a human clicking
///                                through assets, stops scripted flooding.
public static class RateLimitingExtensions
{
    private static readonly string[] PolicyNames = ["auth", "register", "public-sensitive", "compute", "odds"];

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services, IWebHostEnvironment environment)
    {
        // Integration tests must never be throttled
        if (environment.IsEnvironment("Testing"))
        {
            services.AddRateLimiter(options =>
            {
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                    RateLimitPartition.GetNoLimiter(string.Empty));
                foreach (var policy in PolicyNames)
                    options.AddPolicy(policy, _ => RateLimitPartition.GetNoLimiter(string.Empty));
            });
            return services;
        }

        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                FixedWindowByIp(ctx, permitLimit: 300, window: TimeSpan.FromMinutes(1)));

            options.AddPolicy("auth", ctx =>
                FixedWindowByIp(ctx, permitLimit: 10, window: TimeSpan.FromMinutes(1)));

            options.AddPolicy("register", ctx =>
                FixedWindowByIp(ctx, permitLimit: 5, window: TimeSpan.FromHours(1)));

            options.AddPolicy("public-sensitive", ctx =>
                FixedWindowByIp(ctx, permitLimit: 20, window: TimeSpan.FromMinutes(1)));

            options.AddPolicy("compute", ctx =>
                FixedWindowByIp(ctx, permitLimit: 10, window: TimeSpan.FromMinutes(1)));

            options.AddPolicy("odds", ctx =>
                FixedWindowByIp(ctx, permitLimit: 30, window: TimeSpan.FromMinutes(1)));

            options.OnRejected = async (ctx, ct) =>
            {
                ctx.HttpContext.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                ctx.HttpContext.Response.ContentType = MediaTypeNames.Application.Json;
                var body = JsonSerializer.Serialize(new { message = "Too many requests. Please try again later." });
                await ctx.HttpContext.Response.WriteAsync(body, ct);
            };
        });

        return services;
    }

    private static RateLimitPartition<string> FixedWindowByIp(HttpContext ctx, int permitLimit, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
}
