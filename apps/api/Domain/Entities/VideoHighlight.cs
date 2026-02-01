namespace T4L.VideoSearch.Api.Domain.Entities;

/// <summary>
/// Supported languages for highlight translations
/// </summary>
public static class SupportedLanguages
{
    public const string English = "en";
    public const string Hindi = "hi";
    public const string Gujarati = "gu";
    public const string Tamil = "ta";
    public const string Telugu = "te";
    public const string Bengali = "bn";
    public const string Marathi = "mr";

    public static readonly string[] All = [English, Hindi, Gujarati, Tamil, Telugu, Bengali, Marathi];

    public static readonly Dictionary<string, string> Names = new()
    {
        { English, "English" },
        { Hindi, "हिंदी (Hindi)" },
        { Gujarati, "ગુજરાતી (Gujarati)" },
        { Tamil, "தமிழ் (Tamil)" },
        { Telugu, "తెలుగు (Telugu)" },
        { Bengali, "বাংলা (Bengali)" },
        { Marathi, "मराठी (Marathi)" }
    };

    public static bool IsSupported(string code) => All.Contains(code.ToLowerInvariant());

    public static Dictionary<string, string> GetAll() => Names;

    public static string GetName(string code) => Names.TryGetValue(code.ToLowerInvariant(), out var name) ? name : code;
}

/// <summary>
/// Represents an AI-extracted highlight from a video.
/// Text is always stored in English for searchability.
/// Original language text and translations are stored separately.
/// </summary>
public class VideoHighlight
{
    public Guid Id { get; set; }
    public Guid VideoId { get; set; }

    /// <summary>
    /// English text for search indexing (always in English)
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Original text in the source video's language
    /// </summary>
    public string? OriginalText { get; set; }

    /// <summary>
    /// Source language code (e.g., "hi", "gu", "ta")
    /// </summary>
    public string? SourceLanguage { get; set; }

    public string Category { get; set; } = string.Empty; // key_point, promise, announcement, statistic, quote
    public float? Importance { get; set; }
    public long? TimestampMs { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public VideoAsset? Video { get; set; }
    public ICollection<HighlightTranslation> Translations { get; set; } = [];
}

/// <summary>
/// Stores translations of highlights in different languages
/// </summary>
public class HighlightTranslation
{
    public Guid Id { get; set; }
    public Guid VideoHighlightId { get; set; }
    public string LanguageCode { get; set; } = string.Empty; // "hi", "gu", "ta", "te", "bn", "mr"
    public string TranslatedText { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public VideoHighlight? Highlight { get; set; }
}

/// <summary>
/// Represents an AI-generated summary of a video
/// </summary>
public class VideoSummary
{
    public Guid Id { get; set; }
    public Guid VideoId { get; set; }

    /// <summary>
    /// Summary in English for search indexing
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// TL;DR in English for search indexing
    /// </summary>
    public string? TlDr { get; set; }

    /// <summary>
    /// Original summary in source language
    /// </summary>
    public string? OriginalSummary { get; set; }

    /// <summary>
    /// Original TL;DR in source language
    /// </summary>
    public string? OriginalTlDr { get; set; }

    /// <summary>
    /// Source language code
    /// </summary>
    public string? SourceLanguage { get; set; }

    public string[] Keywords { get; set; } = [];
    public string[] Topics { get; set; } = [];
    public string? Sentiment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public VideoAsset? Video { get; set; }
}
