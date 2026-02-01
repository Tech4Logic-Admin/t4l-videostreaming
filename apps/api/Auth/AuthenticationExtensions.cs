using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;

namespace T4L.VideoSearch.Api.Auth;

/// <summary>
/// Extension methods for configuring authentication
/// </summary>
public static class AuthenticationExtensions
{
    /// <summary>
    /// Adds authentication and authorization services
    /// </summary>
    public static IServiceCollection AddAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var useDevAuth = configuration.GetValue<bool>("FeatureFlags:UseDevAuth");

        if (useDevAuth)
        {
            // Development authentication - uses headers for user info
            services.AddAuthentication(DevAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, DevAuthHandler>(DevAuthHandler.SchemeName, null);
        }
        else
        {
            // Production authentication - Microsoft Entra ID (Azure AD)
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApi(configuration.GetSection("AzureAd"));

            // Configure JWT Bearer options
            services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters.RoleClaimType = "roles";
                options.TokenValidationParameters.NameClaimType = "name";
            });
        }

        // Add authorization policies
        services.AddAuthorization(options =>
        {
            // Role-specific policies
            options.AddPolicy(Policies.RequireAdmin, policy =>
                policy.RequireRole(Roles.Admin));

            options.AddPolicy(Policies.RequireUploader, policy =>
                policy.RequireRole(Roles.Uploader));

            options.AddPolicy(Policies.RequireReviewer, policy =>
                policy.RequireRole(Roles.Reviewer));

            options.AddPolicy(Policies.RequireViewer, policy =>
                policy.RequireRole(Roles.Viewer));

            // Composite policies
            options.AddPolicy(Policies.CanUpload, policy =>
                policy.RequireRole(Roles.CanUpload));

            options.AddPolicy(Policies.CanReview, policy =>
                policy.RequireRole(Roles.CanReview));

            options.AddPolicy(Policies.CanViewVideos, policy =>
                policy.RequireRole(Roles.CanView));

            // Default policy - require authenticated user
            options.DefaultPolicy = options.GetPolicy(Policies.CanViewVideos)!;
        });

        // Register current user service
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        return services;
    }
}
