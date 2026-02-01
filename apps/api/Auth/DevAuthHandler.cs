using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace T4L.VideoSearch.Api.Auth;

/// <summary>
/// Development authentication handler that allows testing without Entra ID
/// </summary>
public class DevAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "DevAuth";
    public const string DevUserHeader = "X-Dev-User";
    public const string DevRoleHeader = "X-Dev-Role";

    public DevAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check for dev user header
        if (!Request.Headers.TryGetValue(DevUserHeader, out var userHeader) ||
            string.IsNullOrWhiteSpace(userHeader))
        {
            // For dev mode, allow anonymous with a default dev user
            return Task.FromResult(CreateDevUserResult("dev-user-001", "dev@tech4logic.com", "Dev User", Roles.Admin));
        }

        var userId = userHeader.ToString();
        var email = $"{userId}@tech4logic.com";
        var name = userId.Replace("-", " ").Replace("_", " ");

        // Get role from header or default to Viewer
        var role = Roles.Viewer;
        if (Request.Headers.TryGetValue(DevRoleHeader, out var roleHeader) &&
            !string.IsNullOrWhiteSpace(roleHeader))
        {
            role = roleHeader.ToString();
        }

        return Task.FromResult(CreateDevUserResult(userId, email, name, role));
    }

    private AuthenticateResult CreateDevUserResult(string userId, string email, string name, string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, name),
            new("oid", userId), // Azure AD Object ID
            new("preferred_username", email),
            new(ClaimTypes.Role, role),
            new("roles", role),
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        Logger.LogInformation("Dev auth: User={UserId}, Email={Email}, Role={Role}", userId, email, role);

        return AuthenticateResult.Success(ticket);
    }
}

/// <summary>
/// Pre-defined dev users for testing
/// </summary>
public static class DevUsers
{
    public static readonly (string Id, string Email, string Name, string Role)[] Users =
    {
        ("dev-admin-001", "admin@tech4logic.com", "Dev Admin", Roles.Admin),
        ("dev-uploader-001", "uploader@tech4logic.com", "Dev Uploader", Roles.Uploader),
        ("dev-reviewer-001", "reviewer@tech4logic.com", "Dev Reviewer", Roles.Reviewer),
        ("dev-viewer-001", "viewer@tech4logic.com", "Dev Viewer", Roles.Viewer),
    };
}
