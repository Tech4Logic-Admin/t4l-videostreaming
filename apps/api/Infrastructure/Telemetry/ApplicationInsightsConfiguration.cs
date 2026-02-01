using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;

namespace T4L.VideoSearch.Api.Infrastructure.Telemetry;

/// <summary>
/// Application Insights configuration and telemetry services
/// </summary>
public static class ApplicationInsightsConfiguration
{
    public static IServiceCollection AddCustomApplicationInsights(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

        if (string.IsNullOrEmpty(connectionString))
        {
            // Application Insights not configured, skip
            return services;
        }

        services.AddApplicationInsightsTelemetry(options =>
        {
            options.ConnectionString = connectionString;
            options.EnableAdaptiveSampling = true;
            options.EnableDependencyTrackingTelemetryModule = true;
            options.EnableRequestTrackingTelemetryModule = true;
            options.EnablePerformanceCounterCollectionModule = true;
        });

        // Add custom telemetry initializer
        services.AddSingleton<ITelemetryInitializer, CustomTelemetryInitializer>();

        // Add telemetry processor for filtering
        services.AddApplicationInsightsTelemetryProcessor<FilteringTelemetryProcessor>();

        return services;
    }
}

/// <summary>
/// Custom telemetry initializer to add application-specific properties
/// </summary>
public class CustomTelemetryInitializer : ITelemetryInitializer
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _environmentName;
    private readonly string _applicationVersion;

    public CustomTelemetryInitializer(
        IHttpContextAccessor httpContextAccessor,
        IWebHostEnvironment environment)
    {
        _httpContextAccessor = httpContextAccessor;
        _environmentName = environment.EnvironmentName;
        _applicationVersion = typeof(CustomTelemetryInitializer).Assembly
            .GetName().Version?.ToString() ?? "1.0.0";
    }

    public void Initialize(ITelemetry telemetry)
    {
        // Add cloud role name
        telemetry.Context.Cloud.RoleName = "t4l-videosearch-api";
        telemetry.Context.Cloud.RoleInstance = Environment.MachineName;

        // Add application version
        telemetry.Context.Component.Version = _applicationVersion;

        // Add custom properties
        if (telemetry is ISupportProperties supportProperties)
        {
            supportProperties.Properties["Environment"] = _environmentName;

            // Add correlation ID from HTTP context
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                // Add user information (anonymized)
                var userId = httpContext.User?.Identity?.Name;
                if (!string.IsNullOrEmpty(userId))
                {
                    // Hash the user ID for privacy
                    supportProperties.Properties["UserId"] = ComputeHash(userId);
                }

                // Add tenant ID if present
                var tenantId = httpContext.User?.FindFirst("tenant_id")?.Value;
                if (!string.IsNullOrEmpty(tenantId))
                {
                    supportProperties.Properties["TenantId"] = tenantId;
                }

                // Add correlation ID
                if (httpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId))
                {
                    supportProperties.Properties["CorrelationId"] = correlationId.ToString();
                }
            }
        }
    }

    private static string ComputeHash(string input)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash)[..12]; // Take first 12 chars
    }
}

/// <summary>
/// Telemetry processor for filtering out noise
/// </summary>
public class FilteringTelemetryProcessor : ITelemetryProcessor
{
    private readonly ITelemetryProcessor _next;

    public FilteringTelemetryProcessor(ITelemetryProcessor next)
    {
        _next = next;
    }

    public void Process(ITelemetry item)
    {
        // Filter out health check requests
        if (item is RequestTelemetry request)
        {
            if (request.Url?.AbsolutePath?.StartsWith("/health") == true ||
                request.Url?.AbsolutePath?.StartsWith("/readyz") == true ||
                request.Url?.AbsolutePath?.StartsWith("/livez") == true)
            {
                return; // Don't track health checks
            }
        }

        // Filter out dependency calls to health endpoints
        if (item is DependencyTelemetry dependency)
        {
            if (dependency.Name?.Contains("health", StringComparison.OrdinalIgnoreCase) == true)
            {
                return;
            }
        }

        _next.Process(item);
    }
}

/// <summary>
/// Custom metrics service for business-level telemetry
/// </summary>
public interface IMetricsService
{
    void TrackVideoUpload(string tenantId, long fileSizeBytes, TimeSpan duration);
    void TrackVideoProcessing(string tenantId, string processingType, TimeSpan duration, bool success);
    void TrackSearch(string tenantId, string query, int resultCount, TimeSpan duration);
    void TrackModeration(string tenantId, string videoId, string decision, double confidenceScore);
    void TrackApiCall(string endpoint, string method, int statusCode, TimeSpan duration);
    void TrackException(Exception exception, IDictionary<string, string>? properties = null);
}

/// <summary>
/// Application Insights implementation of metrics service
/// </summary>
public class ApplicationInsightsMetricsService : IMetricsService
{
    private readonly TelemetryClient _telemetryClient;
    private readonly ILogger<ApplicationInsightsMetricsService> _logger;

    public ApplicationInsightsMetricsService(
        TelemetryClient telemetryClient,
        ILogger<ApplicationInsightsMetricsService> logger)
    {
        _telemetryClient = telemetryClient;
        _logger = logger;
    }

    public void TrackVideoUpload(string tenantId, long fileSizeBytes, TimeSpan duration)
    {
        var properties = new Dictionary<string, string>
        {
            ["TenantId"] = tenantId,
            ["FileSizeMB"] = (fileSizeBytes / (1024.0 * 1024.0)).ToString("F2")
        };

        var metrics = new Dictionary<string, double>
        {
            ["FileSizeBytes"] = fileSizeBytes,
            ["DurationMs"] = duration.TotalMilliseconds
        };

        _telemetryClient.TrackEvent("VideoUpload", properties, metrics);
        _logger.LogInformation("Video upload tracked: {FileSizeMB}MB in {DurationMs}ms",
            fileSizeBytes / (1024.0 * 1024.0), duration.TotalMilliseconds);
    }

    public void TrackVideoProcessing(string tenantId, string processingType, TimeSpan duration, bool success)
    {
        var properties = new Dictionary<string, string>
        {
            ["TenantId"] = tenantId,
            ["ProcessingType"] = processingType,
            ["Success"] = success.ToString()
        };

        var metrics = new Dictionary<string, double>
        {
            ["DurationMs"] = duration.TotalMilliseconds
        };

        _telemetryClient.TrackEvent("VideoProcessing", properties, metrics);
    }

    public void TrackSearch(string tenantId, string query, int resultCount, TimeSpan duration)
    {
        var properties = new Dictionary<string, string>
        {
            ["TenantId"] = tenantId,
            ["QueryLength"] = query.Length.ToString(),
            ["HasResults"] = (resultCount > 0).ToString()
        };

        var metrics = new Dictionary<string, double>
        {
            ["ResultCount"] = resultCount,
            ["DurationMs"] = duration.TotalMilliseconds
        };

        _telemetryClient.TrackEvent("SearchQuery", properties, metrics);
    }

    public void TrackModeration(string tenantId, string videoId, string decision, double confidenceScore)
    {
        var properties = new Dictionary<string, string>
        {
            ["TenantId"] = tenantId,
            ["VideoId"] = videoId,
            ["Decision"] = decision
        };

        var metrics = new Dictionary<string, double>
        {
            ["ConfidenceScore"] = confidenceScore
        };

        _telemetryClient.TrackEvent("ContentModeration", properties, metrics);
    }

    public void TrackApiCall(string endpoint, string method, int statusCode, TimeSpan duration)
    {
        var properties = new Dictionary<string, string>
        {
            ["Endpoint"] = endpoint,
            ["Method"] = method,
            ["StatusCode"] = statusCode.ToString(),
            ["IsSuccess"] = (statusCode >= 200 && statusCode < 400).ToString()
        };

        var metrics = new Dictionary<string, double>
        {
            ["DurationMs"] = duration.TotalMilliseconds
        };

        _telemetryClient.TrackEvent("ApiCall", properties, metrics);
    }

    public void TrackException(Exception exception, IDictionary<string, string>? properties = null)
    {
        _telemetryClient.TrackException(exception, properties);
    }
}

/// <summary>
/// Null implementation for when Application Insights is not configured
/// </summary>
public class NullMetricsService : IMetricsService
{
    public void TrackVideoUpload(string tenantId, long fileSizeBytes, TimeSpan duration) { }
    public void TrackVideoProcessing(string tenantId, string processingType, TimeSpan duration, bool success) { }
    public void TrackSearch(string tenantId, string query, int resultCount, TimeSpan duration) { }
    public void TrackModeration(string tenantId, string videoId, string decision, double confidenceScore) { }
    public void TrackApiCall(string endpoint, string method, int statusCode, TimeSpan duration) { }
    public void TrackException(Exception exception, IDictionary<string, string>? properties = null) { }
}
