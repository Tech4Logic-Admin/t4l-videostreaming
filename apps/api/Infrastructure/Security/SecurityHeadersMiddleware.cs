namespace T4L.VideoSearch.Api.Infrastructure.Security;

/// <summary>
/// Middleware to add security headers to all responses
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SecurityHeaderSettings _settings;

    public SecurityHeadersMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _settings = configuration.GetSection("SecurityHeaders").Get<SecurityHeaderSettings>() ?? new SecurityHeaderSettings();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers before response is sent
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            // Prevent MIME type sniffing
            headers["X-Content-Type-Options"] = "nosniff";

            // Prevent clickjacking
            headers["X-Frame-Options"] = _settings.FrameOptions;

            // XSS Protection (legacy, but still useful)
            headers["X-XSS-Protection"] = "1; mode=block";

            // Referrer Policy
            headers["Referrer-Policy"] = _settings.ReferrerPolicy;

            // Content Security Policy
            if (!string.IsNullOrEmpty(_settings.ContentSecurityPolicy))
            {
                headers["Content-Security-Policy"] = _settings.ContentSecurityPolicy;
            }

            // Permissions Policy (feature policy replacement)
            if (!string.IsNullOrEmpty(_settings.PermissionsPolicy))
            {
                headers["Permissions-Policy"] = _settings.PermissionsPolicy;
            }

            // HSTS for HTTPS connections
            if (context.Request.IsHttps && _settings.EnableHsts)
            {
                headers["Strict-Transport-Security"] = $"max-age={_settings.HstsMaxAgeSeconds}; includeSubDomains";
            }

            // Cache control for API responses (configurable per endpoint)
            if (!headers.ContainsKey("Cache-Control"))
            {
                headers["Cache-Control"] = _settings.DefaultCacheControl;
            }

            // Remove server version header
            headers.Remove("Server");
            headers.Remove("X-Powered-By");

            return Task.CompletedTask;
        });

        await _next(context);
    }
}

/// <summary>
/// Settings for security headers
/// </summary>
public class SecurityHeaderSettings
{
    public string FrameOptions { get; set; } = "DENY";
    public string ReferrerPolicy { get; set; } = "strict-origin-when-cross-origin";
    public string ContentSecurityPolicy { get; set; } = "default-src 'self'; img-src 'self' data: https:; style-src 'self' 'unsafe-inline'";
    public string PermissionsPolicy { get; set; } = "camera=(), microphone=(), geolocation=()";
    public bool EnableHsts { get; set; } = true;
    public int HstsMaxAgeSeconds { get; set; } = 31536000; // 1 year
    public string DefaultCacheControl { get; set; } = "no-store, no-cache, must-revalidate";
}

/// <summary>
/// Extension method for adding security headers middleware
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
