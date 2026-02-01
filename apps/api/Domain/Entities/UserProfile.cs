namespace T4L.VideoSearch.Api.Domain.Entities;

/// <summary>
/// Cached user profile from Entra ID
/// </summary>
public class UserProfile
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public string Oid { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string[] GroupIds { get; set; } = [];
    public UserRole Role { get; set; } = UserRole.Viewer;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
}

public enum UserRole
{
    Viewer,
    Uploader,
    Reviewer,
    Admin
}
