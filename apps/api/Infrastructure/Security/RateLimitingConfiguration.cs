using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace T4L.VideoSearch.Api.Infrastructure.Security;

/// <summary>
/// Rate limiting configuration for API endpoints
/// </summary>
public static class RateLimitingConfiguration
{
    public const string GlobalPolicy = "global";
    public const string UploadPolicy = "upload";
    public const string SearchPolicy = "search";
    public const string AuthPolicy = "auth";

    public static IServiceCollection AddRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection("RateLimiting").Get<RateLimitSettings>() ?? new RateLimitSettings();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Global rate limit - applies to all requests
            options.AddFixedWindowLimiter(GlobalPolicy, limiter =>
            {
                limiter.Window = TimeSpan.FromSeconds(settings.GlobalWindowSeconds);
                limiter.PermitLimit = settings.GlobalRequestLimit;
                limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiter.QueueLimit = settings.GlobalQueueLimit;
            });

            // Upload rate limit - stricter for resource-intensive operations
            options.AddFixedWindowLimiter(UploadPolicy, limiter =>
            {
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.PermitLimit = settings.UploadLimitPerMinute;
                limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiter.QueueLimit = 2;
            });

            // Search rate limit - allow bursts for search operations
            options.AddSlidingWindowLimiter(SearchPolicy, limiter =>
            {
                limiter.Window = TimeSpan.FromSeconds(30);
                limiter.PermitLimit = settings.SearchLimitPerWindow;
                limiter.SegmentsPerWindow = 3;
                limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiter.QueueLimit = 5;
            });

            // Auth rate limit - strict to prevent brute force
            options.AddFixedWindowLimiter(AuthPolicy, limiter =>
            {
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.PermitLimit = settings.AuthLimitPerMinute;
                limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiter.QueueLimit = 0; // No queuing for auth
            });

            // Per-user rate limiting using user ID from claims
            options.AddPolicy("per-user", context =>
            {
                var userId = context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
                return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
                {
                    Window = TimeSpan.FromSeconds(settings.PerUserWindowSeconds),
                    PermitLimit = settings.PerUserRequestLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 10
                });
            });

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.Headers.RetryAfter = GetRetryAfterSeconds(context.Lease).ToString();

                var logger = context.HttpContext.RequestServices.GetService<ILogger<RateLimitSettings>>();
                logger?.LogWarning(
                    "Rate limit exceeded for {Path} from {IpAddress} user {User}",
                    context.HttpContext.Request.Path,
                    context.HttpContext.Connection.RemoteIpAddress,
                    context.HttpContext.User?.Identity?.Name ?? "anonymous");

                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    error = "Too many requests",
                    message = "Rate limit exceeded. Please try again later.",
                    retryAfterSeconds = GetRetryAfterSeconds(context.Lease)
                }, cancellationToken);
            };
        });

        return services;
    }

    private static int GetRetryAfterSeconds(RateLimitLease lease)
    {
        if (lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            return (int)retryAfter.TotalSeconds;
        }
        return 60; // Default retry after 60 seconds
    }
}

/// <summary>
/// Rate limiting settings loaded from configuration
/// </summary>
public class RateLimitSettings
{
    public int GlobalWindowSeconds { get; set; } = 10;
    public int GlobalRequestLimit { get; set; } = 100;
    public int GlobalQueueLimit { get; set; } = 10;
    public int UploadLimitPerMinute { get; set; } = 10;
    public int SearchLimitPerWindow { get; set; } = 50;
    public int AuthLimitPerMinute { get; set; } = 10;
    public int PerUserWindowSeconds { get; set; } = 10;
    public int PerUserRequestLimit { get; set; } = 50;
}
