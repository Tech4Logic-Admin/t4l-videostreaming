namespace T4L.VideoSearch.Api.Infrastructure.Adapters;

/// <summary>
/// Abstraction for content safety/moderation service
/// </summary>
public interface IContentSafetyClient
{
    /// <summary>
    /// Analyze text for content safety violations
    /// </summary>
    Task<ContentSafetyResult> AnalyzeTextAsync(string text, CancellationToken ct = default);

    /// <summary>
    /// Analyze an image for content safety violations
    /// </summary>
    Task<ContentSafetyResult> AnalyzeImageAsync(byte[] imageData, CancellationToken ct = default);

    /// <summary>
    /// Analyze an image from URL for content safety violations
    /// </summary>
    Task<ContentSafetyResult> AnalyzeImageUrlAsync(string imageUrl, CancellationToken ct = default);
}

public record ContentSafetyResult(
    bool IsSafe,
    ContentSafetySeverity OverallSeverity,
    IReadOnlyList<ContentSafetyCategory> Categories
);

public enum ContentSafetySeverity
{
    None,
    Low,
    Medium,
    High
}

public record ContentSafetyCategory(
    string Category,
    ContentSafetySeverity Severity,
    float Score
);
