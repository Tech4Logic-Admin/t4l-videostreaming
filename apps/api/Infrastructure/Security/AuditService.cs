using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using T4L.VideoSearch.Api.Auth;
using T4L.VideoSearch.Api.Domain.Entities;
using T4L.VideoSearch.Api.Infrastructure.Persistence;

namespace T4L.VideoSearch.Api.Infrastructure.Security;

/// <summary>
/// Service for recording audit logs of security-relevant actions
/// </summary>
public interface IAuditService
{
    Task LogAsync(string action, string targetType, Guid? targetId = null, object? metadata = null, CancellationToken cancellationToken = default);
    Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of audit logging service
/// </summary>
public class AuditService : IAuditService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        AppDbContext dbContext,
        ICurrentUser currentUser,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditService> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public Task LogAsync(string action, string targetType, Guid? targetId = null, object? metadata = null, CancellationToken cancellationToken = default)
    {
        return LogAsync(new AuditLogEntry
        {
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            Metadata = metadata
        }, cancellationToken);
    }

    public async Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var ipAddress = GetClientIpAddress(httpContext);
            var userAgent = httpContext?.Request.Headers.UserAgent.ToString();

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = null, // Multi-tenant support can be added later
                ActorOid = _currentUser.Id ?? "system",
                Action = entry.Action,
                TargetType = entry.TargetType,
                TargetId = entry.TargetId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Metadata = entry.Metadata != null
                    ? JsonDocument.Parse(JsonSerializer.Serialize(entry.Metadata))
                    : null,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.AuditLogs.Add(auditLog);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Audit: {Action} on {TargetType} {TargetId} by {ActorOid} from {IpAddress}",
                entry.Action,
                entry.TargetType,
                entry.TargetId,
                auditLog.ActorOid,
                ipAddress);
        }
        catch (Exception ex)
        {
            // Don't fail the main operation if audit logging fails
            _logger.LogError(ex,
                "Failed to write audit log for action {Action} on {TargetType}",
                entry.Action,
                entry.TargetType);
        }
    }

    private static string? GetClientIpAddress(HttpContext? context)
    {
        if (context == null) return null;

        // Check for forwarded IP (behind proxy/load balancer)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // Take the first IP in the chain (original client)
            return forwardedFor.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }
}

/// <summary>
/// Entry for audit logging
/// </summary>
public class AuditLogEntry
{
    public string Action { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public Guid? TargetId { get; set; }
    public object? Metadata { get; set; }
}

/// <summary>
/// Audit filter attribute for automatic action logging
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class AuditAttribute : Attribute
{
    public string Action { get; }
    public string TargetType { get; }

    public AuditAttribute(string action, string targetType)
    {
        Action = action;
        TargetType = targetType;
    }
}

/// <summary>
/// Action filter for automatic audit logging
/// </summary>
public class AuditActionFilter : IAsyncActionFilter
{
    private readonly IAuditService _auditService;
    private readonly ILogger<AuditActionFilter> _logger;

    public AuditActionFilter(IAuditService auditService, ILogger<AuditActionFilter> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var auditAttribute = context.ActionDescriptor.EndpointMetadata
            .OfType<AuditAttribute>()
            .FirstOrDefault();

        // Execute the action
        var executedContext = await next();

        // Only log successful actions
        if (auditAttribute != null && executedContext.Exception == null)
        {
            Guid? targetId = null;

            // Try to extract target ID from route
            if (context.RouteData.Values.TryGetValue("id", out var idValue) ||
                context.RouteData.Values.TryGetValue("videoId", out idValue))
            {
                if (Guid.TryParse(idValue?.ToString(), out var parsedId))
                {
                    targetId = parsedId;
                }
            }

            await _auditService.LogAsync(auditAttribute.Action, auditAttribute.TargetType, targetId);
        }
    }
}

/// <summary>
/// Extension methods for audit logging
/// </summary>
public static class AuditServiceExtensions
{
    public static Task LogVideoUploadedAsync(this IAuditService auditService, Guid videoId, string fileName, long fileSize, CancellationToken ct = default)
        => auditService.LogAsync(AuditActions.VideoUploaded, "Video", videoId, new { fileName, fileSize }, ct);

    public static Task LogVideoViewedAsync(this IAuditService auditService, Guid videoId, CancellationToken ct = default)
        => auditService.LogAsync(AuditActions.VideoViewed, "Video", videoId, null, ct);

    public static Task LogVideoPlayedAsync(this IAuditService auditService, Guid videoId, long? positionMs = null, CancellationToken ct = default)
        => auditService.LogAsync(AuditActions.VideoPlayed, "Video", videoId, new { positionMs }, ct);

    public static Task LogVideoSearchAsync(this IAuditService auditService, string query, int resultCount, CancellationToken ct = default)
        => auditService.LogAsync(AuditActions.VideoSearched, "Search", null, new { query, resultCount }, ct);

    public static Task LogVideoApprovedAsync(this IAuditService auditService, Guid videoId, string? notes = null, CancellationToken ct = default)
        => auditService.LogAsync(AuditActions.VideoApproved, "Video", videoId, new { notes }, ct);

    public static Task LogVideoRejectedAsync(this IAuditService auditService, Guid videoId, string? notes = null, CancellationToken ct = default)
        => auditService.LogAsync(AuditActions.VideoRejected, "Video", videoId, new { notes }, ct);

    public static Task LogVideoDeletedAsync(this IAuditService auditService, Guid videoId, string? reason = null, CancellationToken ct = default)
        => auditService.LogAsync(AuditActions.VideoDeleted, "Video", videoId, new { reason }, ct);

    public static Task LogUserLoginAsync(this IAuditService auditService, string method = "token", CancellationToken ct = default)
        => auditService.LogAsync(AuditActions.UserLogin, "User", null, new { method }, ct);

    public static Task LogSettingsChangedAsync(this IAuditService auditService, string settingName, object? oldValue, object? newValue, CancellationToken ct = default)
        => auditService.LogAsync(AuditActions.SettingsChanged, "Settings", null, new { settingName, oldValue, newValue }, ct);
}
