using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using T4L.VideoSearch.Api.Domain.Entities;

namespace T4L.VideoSearch.Api.Infrastructure.Services;

/// <summary>
/// Service for translating text between supported Indian languages
/// </summary>
public interface ITranslationService
{
    /// <summary>
    /// Translate text to English from any supported language
    /// </summary>
    Task<TranslationResult> TranslateToEnglishAsync(string text, string? sourceLanguage = null, CancellationToken ct = default);

    /// <summary>
    /// Translate text from English to a target language
    /// </summary>
    Task<TranslationResult> TranslateFromEnglishAsync(string text, string targetLanguage, CancellationToken ct = default);

    /// <summary>
    /// Translate text between any two supported languages
    /// </summary>
    Task<TranslationResult> TranslateAsync(string text, string sourceLanguage, string targetLanguage, CancellationToken ct = default);

    /// <summary>
    /// Batch translate multiple texts to a target language
    /// </summary>
    Task<List<TranslationResult>> BatchTranslateAsync(IEnumerable<string> texts, string sourceLanguage, string targetLanguage, CancellationToken ct = default);

    /// <summary>
    /// Detect the language of the given text
    /// </summary>
    Task<LanguageDetectionResult> DetectLanguageAsync(string text, CancellationToken ct = default);
}

public class TranslationResult
{
    public bool Success { get; init; }
    public string? TranslatedText { get; init; }
    public string? SourceLanguage { get; init; }
    public string? TargetLanguage { get; init; }
    public string? Error { get; init; }
}

public class LanguageDetectionResult
{
    public bool Success { get; init; }
    public string? DetectedLanguage { get; init; }
    public string? LanguageName { get; init; }
    public float? Confidence { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// OpenAI-based translation service supporting Indian languages
/// </summary>
public class OpenAITranslationService : ITranslationService
{
    private readonly ILogger<OpenAITranslationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    private static readonly Dictionary<string, string> LanguageNames = new()
    {
        { "en", "English" },
        { "hi", "Hindi" },
        { "gu", "Gujarati" },
        { "ta", "Tamil" },
        { "te", "Telugu" },
        { "bn", "Bengali" },
        { "mr", "Marathi" }
    };

    public OpenAITranslationService(
        ILogger<OpenAITranslationService> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient("OpenAI");
    }

    public async Task<TranslationResult> TranslateToEnglishAsync(string text, string? sourceLanguage = null, CancellationToken ct = default)
    {
        return await TranslateAsync(text, sourceLanguage ?? "auto", "en", ct);
    }

    public async Task<TranslationResult> TranslateFromEnglishAsync(string text, string targetLanguage, CancellationToken ct = default)
    {
        return await TranslateAsync(text, "en", targetLanguage, ct);
    }

    public async Task<TranslationResult> TranslateAsync(string text, string sourceLanguage, string targetLanguage, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new TranslationResult { Success = false, Error = "Text cannot be empty" };
        }

        // Skip if source and target are the same
        if (sourceLanguage == targetLanguage)
        {
            return new TranslationResult
            {
                Success = true,
                TranslatedText = text,
                SourceLanguage = sourceLanguage,
                TargetLanguage = targetLanguage
            };
        }

        var apiKey = _configuration["OpenAI:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("OpenAI API key not configured");
            return new TranslationResult { Success = false, Error = "Translation service not configured" };
        }

        try
        {
            var sourceLangName = sourceLanguage == "auto" ? "the source language" :
                LanguageNames.GetValueOrDefault(sourceLanguage, sourceLanguage);
            var targetLangName = LanguageNames.GetValueOrDefault(targetLanguage, targetLanguage);

            var systemPrompt = $@"You are an expert translator specializing in Indian languages.
Translate the following text to {targetLangName}.
{(sourceLanguage != "auto" ? $"The source language is {sourceLangName}." : "Auto-detect the source language.")}

Rules:
1. Maintain the original meaning and tone
2. Use natural, fluent {targetLangName}
3. Preserve any technical terms or proper nouns appropriately
4. Return ONLY the translated text, nothing else";

            var requestBody = new
            {
                model = _configuration["OpenAI:Model"] ?? "gpt-4o-mini",
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = text }
                },
                temperature = 0.3,
                max_tokens = 2000
            };

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await _httpClient.PostAsync(
                "https://api.openai.com/v1/chat/completions",
                new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"),
                ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("OpenAI translation error: {Status} - {Error}", response.StatusCode, errorContent);
                return new TranslationResult { Success = false, Error = $"Translation failed: {response.StatusCode}" };
            }

            var jsonContent = await response.Content.ReadAsStringAsync(ct);
            var chatResponse = JsonSerializer.Deserialize<ChatCompletionResponse>(jsonContent);

            var translatedText = chatResponse?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();

            if (string.IsNullOrEmpty(translatedText))
            {
                return new TranslationResult { Success = false, Error = "Empty response from translation service" };
            }

            _logger.LogDebug("Translated text from {Source} to {Target}: {Text}",
                sourceLanguage, targetLanguage, translatedText.Length > 100 ? translatedText[..100] + "..." : translatedText);

            return new TranslationResult
            {
                Success = true,
                TranslatedText = translatedText,
                SourceLanguage = sourceLanguage,
                TargetLanguage = targetLanguage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Translation failed");
            return new TranslationResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<List<TranslationResult>> BatchTranslateAsync(
        IEnumerable<string> texts,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken ct = default)
    {
        var textList = texts.ToList();
        if (!textList.Any())
        {
            return [];
        }

        // For efficiency, batch translate using a single API call with numbered items
        var apiKey = _configuration["OpenAI:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            return textList.Select(t => new TranslationResult
            {
                Success = false,
                Error = "Translation service not configured"
            }).ToList();
        }

        try
        {
            var sourceLangName = LanguageNames.GetValueOrDefault(sourceLanguage, sourceLanguage);
            var targetLangName = LanguageNames.GetValueOrDefault(targetLanguage, targetLanguage);

            var numberedTexts = string.Join("\n", textList.Select((t, i) => $"[{i + 1}] {t}"));

            var systemPrompt = $@"You are an expert translator specializing in Indian languages.
Translate each numbered item from {sourceLangName} to {targetLangName}.
Maintain the numbering format [1], [2], etc. in your response.
Return ONLY the translated texts with their numbers, nothing else.";

            var requestBody = new
            {
                model = _configuration["OpenAI:Model"] ?? "gpt-4o-mini",
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = numberedTexts }
                },
                temperature = 0.3,
                max_tokens = 4000
            };

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await _httpClient.PostAsync(
                "https://api.openai.com/v1/chat/completions",
                new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"),
                ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("OpenAI batch translation error: {Status}", response.StatusCode);
                return textList.Select(t => new TranslationResult
                {
                    Success = false,
                    Error = $"Translation failed: {response.StatusCode}"
                }).ToList();
            }

            var jsonContent = await response.Content.ReadAsStringAsync(ct);
            var chatResponse = JsonSerializer.Deserialize<ChatCompletionResponse>(jsonContent);

            var translatedContent = chatResponse?.Choices?.FirstOrDefault()?.Message?.Content?.Trim() ?? "";

            // Parse the numbered responses
            var results = new List<TranslationResult>();
            var lines = translatedContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < textList.Count; i++)
            {
                var expectedPrefix = $"[{i + 1}]";
                var matchingLine = lines.FirstOrDefault(l => l.Trim().StartsWith(expectedPrefix));

                if (matchingLine != null)
                {
                    var translatedText = matchingLine.Trim()[(expectedPrefix.Length)..].Trim();
                    results.Add(new TranslationResult
                    {
                        Success = true,
                        TranslatedText = translatedText,
                        SourceLanguage = sourceLanguage,
                        TargetLanguage = targetLanguage
                    });
                }
                else
                {
                    // Fallback: try individual translation
                    var individualResult = await TranslateAsync(textList[i], sourceLanguage, targetLanguage, ct);
                    results.Add(individualResult);
                }
            }

            _logger.LogInformation("Batch translated {Count} texts from {Source} to {Target}",
                results.Count(r => r.Success), sourceLanguage, targetLanguage);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch translation failed");
            return textList.Select(t => new TranslationResult { Success = false, Error = ex.Message }).ToList();
        }
    }

    public async Task<LanguageDetectionResult> DetectLanguageAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new LanguageDetectionResult { Success = false, Error = "Text cannot be empty" };
        }

        var apiKey = _configuration["OpenAI:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            return new LanguageDetectionResult { Success = false, Error = "Detection service not configured" };
        }

        try
        {
            var systemPrompt = @"Detect the language of the given text.
Respond in JSON format with:
{
  ""language_code"": ""two-letter ISO 639-1 code"",
  ""language_name"": ""full language name"",
  ""confidence"": 0.0-1.0
}

Supported languages: en (English), hi (Hindi), gu (Gujarati), ta (Tamil), te (Telugu), bn (Bengali), mr (Marathi)
If the language is not in this list, return the closest match or 'unknown'.";

            var requestBody = new
            {
                model = _configuration["OpenAI:Model"] ?? "gpt-4o-mini",
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = text.Length > 500 ? text[..500] : text }
                },
                temperature = 0.1,
                max_tokens = 100,
                response_format = new { type = "json_object" }
            };

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await _httpClient.PostAsync(
                "https://api.openai.com/v1/chat/completions",
                new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"),
                ct);

            if (!response.IsSuccessStatusCode)
            {
                return new LanguageDetectionResult { Success = false, Error = "Detection failed" };
            }

            var jsonContent = await response.Content.ReadAsStringAsync(ct);
            var chatResponse = JsonSerializer.Deserialize<ChatCompletionResponse>(jsonContent);

            var content = chatResponse?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrEmpty(content))
            {
                return new LanguageDetectionResult { Success = false, Error = "Empty response" };
            }

            var detection = JsonSerializer.Deserialize<LanguageDetectionJson>(content);

            return new LanguageDetectionResult
            {
                Success = true,
                DetectedLanguage = detection?.LanguageCode ?? "unknown",
                LanguageName = detection?.LanguageName ?? "Unknown",
                Confidence = detection?.Confidence ?? 0.5f
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Language detection failed");
            return new LanguageDetectionResult { Success = false, Error = ex.Message };
        }
    }

    private class LanguageDetectionJson
    {
        [JsonPropertyName("language_code")]
        public string? LanguageCode { get; set; }

        [JsonPropertyName("language_name")]
        public string? LanguageName { get; set; }

        [JsonPropertyName("confidence")]
        public float? Confidence { get; set; }
    }
}

// Using existing ChatCompletionResponse from AIHighlightsService
