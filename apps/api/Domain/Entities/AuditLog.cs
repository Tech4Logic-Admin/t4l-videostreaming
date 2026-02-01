using System.Text.Json;

namespace T4L.VideoSearch.Api.Domain.Entities;

/// <summary>
/// Audit log for tracking all critical actions
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public string ActorOid { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public Guid? TargetId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public JsonDocument? Metadata { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class AuditActions
{
    public const string VideoUploaded = "video.uploaded";
    public const string VideoViewed = "video.viewed";
    public const string VideoPlayed = "video.played";
    public const string VideoSearched = "video.searched";
    public const string VideoApproved = "video.approved";
    public const string VideoRejected = "video.rejected";
    public const string VideoDeleted = "video.deleted";
    public const string UserLogin = "user.login";
    public const string UserLogout = "user.logout";
    public const string SettingsChanged = "settings.changed";
}
