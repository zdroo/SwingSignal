using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SwingSignal.Api.Extensions;
using SwingSignal.Api.Middleware;
using SwingSignal.Application;
using SwingSignal.Application.Common;
using SwingSignal.Infrastructure;
using SwingSignal.Infrastructure.Persistence;
using SwingSignal.Infrastructure.Seed;

var builder = WebApplication.CreateBuilder(args);

// Fail fast on missing production configuration — a half-configured deploy
// must die at startup, not at the first user's registration email.
if (builder.Environment.IsProduction())
{
    string[] required =
    [
        "ConnectionStrings:SqlServer",
        "Jwt:Secret",
        "Google:ClientId",
        "Resend:ApiKey",
        "Frontend:Url",
    ];
    var missing = required
        .Where(key => string.IsNullOrWhiteSpace(builder.Configuration[key]))
        .ToList();
    if (missing.Count > 0)
        throw new InvalidOperationException(
            $"Missing required production configuration: {string.Join(", ", missing)}");
}

// Behind a reverse proxy the client IP and scheme arrive in X-Forwarded-*;
// without this the rate limiter would throttle the proxy's IP (one bucket
// for every user) and HTTPS redirection could loop. No-op until enabled.
if (builder.Configuration.GetValue<bool>("ForwardedHeaders:Enabled"))
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        // The proxy is the platform's own load balancer — addresses unknowable ahead of time
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(
    builder.Configuration,
    // Background ingestion hits external APIs — never inside integration tests
    includeBackgroundServices: !builder.Environment.IsEnvironment("Testing"));
builder.Services.AddAppRateLimiting(builder.Environment);

builder.Services.AddHealthChecks()
    .AddDbContextCheck<SwingSignalDbContext>();

var frontendUrl = builder.Configuration["Frontend:Url"] ?? "http://localhost:3000";
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(frontendUrl)
     .AllowAnyHeader()
     .AllowAnyMethod()));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]
                    ?? throw new InvalidOperationException("Jwt:Secret not configured"))),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization(options =>
    // The paid boundary: endpoints carry this once their feature launches
    options.AddPolicy("ProOnly", policy => policy.RequireClaim("plan", "Pro")));

// Pro dark-launch switch: entitlement gates exist in code but only bite
// when Features:ProEnabled is true (see ProFeatures)
builder.Services.AddSingleton(new ProFeatures(
    builder.Configuration.GetValue<bool>("Features:ProEnabled")));
builder.Services.AddSingleton<IDailyQuota, InMemoryDailyQuota>();

var app = builder.Build();

// Integration tests provide their own (SQLite) schema and seed data
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SwingSignalDbContext>();
    db.Database.Migrate();

    var seeder = scope.ServiceProvider.GetRequiredService<AssetSeeder>();
    await seeder.SeedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseForwardedHeaders(); // must precede HTTPS redirect and the rate limiter
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Exposes the entry point to WebApplicationFactory in integration tests
public partial class Program { }
