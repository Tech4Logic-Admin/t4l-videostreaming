using System.Security.Claims;

namespace T4L.VideoSearch.Api.Auth;

/// <summary>
/// Represents the current authenticated user
/// </summary>
public interface ICurrentUser
{
    /// <summary>
    /// User's unique identifier (Object ID from Entra ID or dev user ID)
    /// </summary>
    string? Id { get; }

    /// <summary>
    /// User's email address
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// User's display name
    /// </summary>
    string? Name { get; }

    /// <summary>
    /// User's roles
    /// </summary>
    IReadOnlyList<string> Roles { get; }

    /// <summary>
    /// Whether the user is authenticated
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Check if user has a specific role
    /// </summary>
    bool IsInRole(string role);

    /// <summary>
    /// Check if user has any of the specified roles
    /// </summary>
    bool IsInAnyRole(params string[] roles);
}

/// <summary>
/// Implementation that extracts user info from ClaimsPrincipal
/// </summary>
public class CurrentUser : ICurrentUser
{
    private readonly ClaimsPrincipal? _principal;
    private readonly List<string> _roles;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _principal = httpContextAccessor.HttpContext?.User;
        _roles = ExtractRoles();
    }

    public string? Id => _principal?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? _principal?.FindFirstValue("oid")  // Azure AD Object ID
        ?? _principal?.FindFirstValue("sub"); // Subject claim

    public string? Email => _principal?.FindFirstValue(ClaimTypes.Email)
        ?? _principal?.FindFirstValue("preferred_username")
        ?? _principal?.FindFirstValue("email");

    public string? Name => _principal?.FindFirstValue(ClaimTypes.Name)
        ?? _principal?.FindFirstValue("name")
        ?? Email;

    public IReadOnlyList<string> Roles => _roles.AsReadOnly();

    public bool IsAuthenticated => _principal?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role) => _roles.Contains(role, StringComparer.OrdinalIgnoreCase);

    public bool IsInAnyRole(params string[] roles) => roles.Any(r => IsInRole(r));

    private List<string> ExtractRoles()
    {
        if (_principal == null)
            return new List<string>();

        var roles = new List<string>();

        // Extract from role claims (standard)
        roles.AddRange(_principal.FindAll(ClaimTypes.Role).Select(c => c.Value));

        // Extract from "roles" claim (Azure AD)
        roles.AddRange(_principal.FindAll("roles").Select(c => c.Value));

        // Extract from "groups" claim if mapped to roles
        roles.AddRange(_principal.FindAll("groups").Select(c => c.Value));

        // Dev auth may use custom role claim
        roles.AddRange(_principal.FindAll("role").Select(c => c.Value));

        return roles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
