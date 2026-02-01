namespace T4L.VideoSearch.Api.Auth;

/// <summary>
/// Application roles for RBAC
/// </summary>
public static class Roles
{
    /// <summary>
    /// Full system access - user management, configuration, audit logs, reports
    /// </summary>
    public const string Admin = "Admin";

    /// <summary>
    /// Can upload videos, view own videos, basic search
    /// </summary>
    public const string Uploader = "Uploader";

    /// <summary>
    /// Can review flagged content, approve/reject videos, view moderation queue
    /// </summary>
    public const string Reviewer = "Reviewer";

    /// <summary>
    /// Read-only access to approved videos and search
    /// </summary>
    public const string Viewer = "Viewer";

    /// <summary>
    /// All roles as an array
    /// </summary>
    public static readonly string[] All = { Admin, Uploader, Reviewer, Viewer };

    /// <summary>
    /// Roles that can upload content
    /// </summary>
    public static readonly string[] CanUpload = { Admin, Uploader };

    /// <summary>
    /// Roles that can review content
    /// </summary>
    public static readonly string[] CanReview = { Admin, Reviewer };

    /// <summary>
    /// Roles that can view content
    /// </summary>
    public static readonly string[] CanView = { Admin, Uploader, Reviewer, Viewer };
}

/// <summary>
/// Policy names for authorization
/// </summary>
public static class Policies
{
    public const string RequireAdmin = "RequireAdmin";
    public const string RequireUploader = "RequireUploader";
    public const string RequireReviewer = "RequireReviewer";
    public const string RequireViewer = "RequireViewer";
    public const string CanUpload = "CanUpload";
    public const string CanReview = "CanReview";
    public const string CanViewVideos = "CanViewVideos";
}
