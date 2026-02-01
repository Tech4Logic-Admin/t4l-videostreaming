using System.Text.RegularExpressions;
using FluentValidation;

namespace T4L.VideoSearch.Api.Infrastructure.Security;

/// <summary>
/// Input sanitization utilities for preventing XSS and injection attacks
/// </summary>
public static partial class InputSanitizer
{
    // Regex patterns for validation
    [GeneratedRegex(@"<[^>]*>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagsRegex();

    [GeneratedRegex(@"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", RegexOptions.Compiled)]
    private static partial Regex ControlCharsRegex();

    [GeneratedRegex(@"javascript:|data:|vbscript:", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DangerousProtocolsRegex();

    /// <summary>
    /// Sanitize text by removing potentially dangerous content
    /// </summary>
    public static string SanitizeText(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        // Remove HTML tags
        var result = HtmlTagsRegex().Replace(input, string.Empty);

        // Remove control characters
        result = ControlCharsRegex().Replace(result, string.Empty);

        // Remove dangerous protocols
        result = DangerousProtocolsRegex().Replace(result, string.Empty);

        // Trim and limit length
        return result.Trim();
    }

    /// <summary>
    /// Sanitize filename to prevent path traversal
    /// </summary>
    public static string SanitizeFileName(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return "unnamed";

        // Get just the filename, removing any path components
        var name = Path.GetFileName(fileName);

        // Remove potentially dangerous characters
        var invalid = Path.GetInvalidFileNameChars();
        foreach (var c in invalid)
        {
            name = name.Replace(c, '_');
        }

        // Remove sequences that could be dangerous
        name = name.Replace("..", "_");
        name = name.Replace("~", "_");

        // Limit length
        if (name.Length > 255)
        {
            var ext = Path.GetExtension(name);
            name = name[..(255 - ext.Length)] + ext;
        }

        return string.IsNullOrWhiteSpace(name) ? "unnamed" : name;
    }

    /// <summary>
    /// Validate and sanitize URL
    /// </summary>
    public static bool IsValidUrl(string? url, out Uri? validatedUri)
    {
        validatedUri = null;
        if (string.IsNullOrEmpty(url)) return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out validatedUri))
            return false;

        // Only allow HTTP/HTTPS
        return validatedUri.Scheme == Uri.UriSchemeHttp || validatedUri.Scheme == Uri.UriSchemeHttps;
    }

    /// <summary>
    /// Sanitize search query
    /// </summary>
    public static string SanitizeSearchQuery(string? query, int maxLength = 500)
    {
        if (string.IsNullOrEmpty(query)) return string.Empty;

        var sanitized = SanitizeText(query);

        // Limit length
        if (sanitized.Length > maxLength)
        {
            sanitized = sanitized[..maxLength];
        }

        return sanitized;
    }

    /// <summary>
    /// Validate email format
    /// </summary>
    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrEmpty(email)) return false;

        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Request size limiting middleware
/// </summary>
public class RequestSizeLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly long _maxRequestBodySize;
    private readonly ILogger<RequestSizeLimitMiddleware> _logger;

    public RequestSizeLimitMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<RequestSizeLimitMiddleware> logger)
    {
        _next = next;
        _maxRequestBodySize = configuration.GetValue<long>("RequestLimits:MaxBodySizeBytes", 10 * 1024 * 1024); // Default 10MB
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip for file uploads (they have their own limits)
        if (context.Request.Path.StartsWithSegments("/api/upload"))
        {
            await _next(context);
            return;
        }

        // Check content length header
        if (context.Request.ContentLength > _maxRequestBodySize)
        {
            _logger.LogWarning(
                "Request body too large: {ContentLength} bytes from {IpAddress}",
                context.Request.ContentLength,
                context.Connection.RemoteIpAddress);

            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Payload too large",
                message = $"Request body exceeds maximum allowed size of {_maxRequestBodySize / (1024 * 1024)}MB"
            });
            return;
        }

        await _next(context);
    }
}

/// <summary>
/// Common validation rules for FluentValidation
/// </summary>
public static class ValidationRules
{
    public const int MaxTitleLength = 500;
    public const int MaxDescriptionLength = 5000;
    public const int MaxTagLength = 100;
    public const int MaxTagCount = 20;
    public const int MaxSearchQueryLength = 500;
    public const int MaxNotesLength = 2000;
    public const long MaxUploadSize = 5L * 1024 * 1024 * 1024; // 5GB
}

/// <summary>
/// Validator for video metadata
/// </summary>
public class VideoMetadataValidator : AbstractValidator<VideoMetadataInput>
{
    public VideoMetadataValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(ValidationRules.MaxTitleLength)
            .Must(x => !string.IsNullOrEmpty(InputSanitizer.SanitizeText(x)))
            .WithMessage("Title contains invalid content");

        RuleFor(x => x.Description)
            .MaximumLength(ValidationRules.MaxDescriptionLength)
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Tags)
            .Must(x => x == null || x.Length <= ValidationRules.MaxTagCount)
            .WithMessage($"Maximum {ValidationRules.MaxTagCount} tags allowed")
            .Must(x => x == null || x.All(t => t.Length <= ValidationRules.MaxTagLength))
            .WithMessage($"Each tag must be {ValidationRules.MaxTagLength} characters or less");
    }
}

public class VideoMetadataInput
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string[]? Tags { get; set; }
}

/// <summary>
/// Validator for search queries
/// </summary>
public class SearchQueryValidator : AbstractValidator<SearchQueryInput>
{
    public SearchQueryValidator()
    {
        RuleFor(x => x.Query)
            .MaximumLength(ValidationRules.MaxSearchQueryLength)
            .When(x => !string.IsNullOrEmpty(x.Query));

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be 1 or greater");
    }
}

public class SearchQueryInput
{
    public string? Query { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
