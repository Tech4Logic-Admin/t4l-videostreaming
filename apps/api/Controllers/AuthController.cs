using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using T4L.VideoSearch.Api.Auth;

namespace T4L.VideoSearch.Api.Controllers;

/// <summary>
/// Authentication and user profile endpoints
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<AuthController> _logger;

    public AuthController(ICurrentUser currentUser, ILogger<AuthController> logger)
    {
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>
    /// Get the current user's profile
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<UserProfileResponse> GetCurrentUser()
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Unauthorized();
        }

        var response = new UserProfileResponse
        {
            Id = _currentUser.Id ?? "",
            Email = _currentUser.Email ?? "",
            Name = _currentUser.Name ?? "",
            Roles = _currentUser.Roles.ToList(),
            Permissions = GetPermissions()
        };

        _logger.LogInformation("User profile requested: {UserId}", _currentUser.Id);

        return Ok(response);
    }

    /// <summary>
    /// Check if the current user is authenticated
    /// </summary>
    [HttpGet("check")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthCheckResponse), StatusCodes.Status200OK)]
    public ActionResult<AuthCheckResponse> CheckAuth()
    {
        return Ok(new AuthCheckResponse
        {
            IsAuthenticated = _currentUser.IsAuthenticated,
            UserId = _currentUser.Id
        });
    }

    /// <summary>
    /// Get available dev users (only in dev mode)
    /// </summary>
    [HttpGet("dev-users")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(DevUserInfo[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<DevUserInfo[]> GetDevUsers([FromServices] IConfiguration configuration)
    {
        var useDevAuth = configuration.GetValue<bool>("FeatureFlags:UseDevAuth");
        if (!useDevAuth)
        {
            return NotFound("Dev users not available in production mode");
        }

        var devUsers = DevUsers.Users.Select(u => new DevUserInfo
        {
            Id = u.Id,
            Email = u.Email,
            Name = u.Name,
            Role = u.Role
        }).ToArray();

        return Ok(devUsers);
    }

    private UserPermissions GetPermissions()
    {
        return new UserPermissions
        {
            CanUpload = _currentUser.IsInAnyRole(Roles.CanUpload),
            CanReview = _currentUser.IsInAnyRole(Roles.CanReview),
            CanViewVideos = _currentUser.IsInAnyRole(Roles.CanView),
            CanManageUsers = _currentUser.IsInRole(Roles.Admin),
            CanViewAuditLogs = _currentUser.IsInRole(Roles.Admin),
            CanViewReports = _currentUser.IsInRole(Roles.Admin),
            CanConfigureSystem = _currentUser.IsInRole(Roles.Admin)
        };
    }
}

/// <summary>
/// User profile response DTO
/// </summary>
public record UserProfileResponse
{
    public required string Id { get; init; }
    public required string Email { get; init; }
    public required string Name { get; init; }
    public required List<string> Roles { get; init; }
    public required UserPermissions Permissions { get; init; }
}

/// <summary>
/// User permissions based on roles
/// </summary>
public record UserPermissions
{
    public bool CanUpload { get; init; }
    public bool CanReview { get; init; }
    public bool CanViewVideos { get; init; }
    public bool CanManageUsers { get; init; }
    public bool CanViewAuditLogs { get; init; }
    public bool CanViewReports { get; init; }
    public bool CanConfigureSystem { get; init; }
}

/// <summary>
/// Auth check response DTO
/// </summary>
public record AuthCheckResponse
{
    public bool IsAuthenticated { get; init; }
    public string? UserId { get; init; }
}

/// <summary>
/// Dev user info DTO
/// </summary>
public record DevUserInfo
{
    public required string Id { get; init; }
    public required string Email { get; init; }
    public required string Name { get; init; }
    public required string Role { get; init; }
}
