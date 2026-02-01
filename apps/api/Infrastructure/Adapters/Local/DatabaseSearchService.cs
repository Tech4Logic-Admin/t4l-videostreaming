using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using T4L.VideoSearch.Api.Domain.Entities;
using T4L.VideoSearch.Api.Infrastructure.Persistence;

namespace T4L.VideoSearch.Api.Infrastructure.Adapters.Local;

/// <summary>
/// Database-backed search service that performs semantic search across:
/// - Video titles, descriptions, tags
/// - Video summaries and highlights
/// - Transcript segments
/// </summary>
public class DatabaseSearchService : ISearchIndexClient
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<DatabaseSearchService> _logger;

    public DatabaseSearchService(AppDbContext dbContext, ILogger<DatabaseSearchService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<SearchResults> SearchAsync(SearchQuery query, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var queryLower = query.QueryText.ToLowerInvariant().Trim();
        var queryTerms = queryLower.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (queryTerms.Length == 0)
        {
            return new SearchResults([], 0, 0);
        }

        _logger.LogInformation("Database Search: Searching for '{Query}' with {TermCount} terms",
            query.QueryText, queryTerms.Length);

        var allResults = new List<SearchResultItem>();

        // 1. Search video metadata (title, description, tags)
        var videoMatches = await SearchVideoMetadataAsync(queryTerms, query, ct);
        allResults.AddRange(videoMatches);
        _logger.LogDebug("Found {Count} video metadata matches", videoMatches.Count);

        // 2. Search video summaries (summary text, keywords, topics, TLDR)
        var summaryMatches = await SearchVideoSummariesAsync(queryTerms, query, ct);
        allResults.AddRange(summaryMatches);
        _logger.LogDebug("Found {Count} summary matches", summaryMatches.Count);

        // 3. Search video highlights (key points, quotes, announcements)
        var highlightMatches = await SearchVideoHighlightsAsync(queryTerms, query, ct);
        allResults.AddRange(highlightMatches);
        _logger.LogDebug("Found {Count} highlight matches", highlightMatches.Count);

        // 4. Search transcript segments
        var transcriptMatches = await SearchTranscriptSegmentsAsync(queryTerms, query, ct);
        allResults.AddRange(transcriptMatches);
        _logger.LogDebug("Found {Count} transcript matches", transcriptMatches.Count);

        // Group by video and calculate combined scores
        var groupedResults = allResults
            .GroupBy(r => r.VideoId)
            .Select(g => new
            {
                VideoId = g.Key,
                VideoTitle = g.First().VideoTitle,
                Language = g.First().Language,
                // Combine all matches, with best score per video + source type diversity bonus
                BestScore = g.Max(r => r.Score) + (g.Select(r => r.SourceType).Distinct().Count() * 0.2f),
                Items = g.ToList()
            })
            .OrderByDescending(g => g.BestScore);

        var totalCount = groupedResults.Count();

        // Apply sorting and pagination
        var sortedGroups = query.SortOrder switch
        {
            SearchSortOrder.TitleAsc => groupedResults.OrderBy(g => g.VideoTitle),
            SearchSortOrder.TitleDesc => groupedResults.OrderByDescending(g => g.VideoTitle),
            _ => groupedResults.OrderByDescending(g => g.BestScore)
        };

        // Convert to SearchHits - each hit represents a match with timeline info
        var hits = sortedGroups
            .SelectMany(g => g.Items.OrderByDescending(i => i.Score).Select(item => new SearchHit(
                VideoId: item.VideoId,
                VideoTitle: item.VideoTitle,
                SegmentId: item.SegmentId ?? Guid.NewGuid(),
                StartMs: item.StartMs,
                EndMs: item.EndMs,
                Text: item.Text,
                HighlightedText: HighlightText(item.Text, queryTerms),
                Score: item.Score,
                Language: item.Language
            )))
            .Skip(query.Skip)
            .Take(query.Take)
            .ToList();

        sw.Stop();

        _logger.LogInformation("Database Search: Query '{Query}' returned {Count} results from {TotalVideos} videos in {Ms}ms",
            query.QueryText, hits.Count, totalCount, sw.ElapsedMilliseconds);

        // Build facets from all matching videos
        var facets = await BuildFacetsAsync(query.UserOid, query.UserGroupIds, ct);

        return new SearchResults(
            Hits: hits,
            TotalCount: totalCount,
            LatencyMs: sw.ElapsedMilliseconds,
            Facets: facets
        );
    }

    private async Task<List<SearchResultItem>> SearchVideoMetadataAsync(string[] queryTerms, SearchQuery query, CancellationToken ct)
    {
        var results = new List<SearchResultItem>();

        var videosQuery = _dbContext.VideoAssets
            .Where(v => v.Status == VideoStatus.Published || v.CreatedByOid == query.UserOid);

        if (query.VideoIdFilter.HasValue)
        {
            videosQuery = videosQuery.Where(v => v.Id == query.VideoIdFilter.Value);
        }

        var videos = await videosQuery.ToListAsync(ct);

        foreach (var video in videos)
        {
            var titleLower = video.Title.ToLowerInvariant();
            var descriptionLower = (video.Description ?? "").ToLowerInvariant();
            var tagsLower = string.Join(" ", video.Tags).ToLowerInvariant();

            var score = CalculateScore(titleLower, queryTerms, 3.0f); // Title has highest weight
            score += CalculateScore(descriptionLower, queryTerms, 1.5f);
            score += CalculateScore(tagsLower, queryTerms, 2.0f);

            if (score > 0)
            {
                // Create a match text that shows what matched
                var matchText = !string.IsNullOrEmpty(video.Description)
                    ? video.Description
                    : $"Video: {video.Title}" + (video.Tags.Length > 0 ? $" (Tags: {string.Join(", ", video.Tags)})" : "");

                results.Add(new SearchResultItem
                {
                    VideoId = video.Id,
                    VideoTitle = video.Title,
                    SegmentId = null,
                    StartMs = 0,
                    EndMs = 0,
                    Text = matchText,
                    Score = score,
                    Language = video.LanguageHint,
                    SourceType = "metadata"
                });
            }
        }

        return results;
    }

    private async Task<List<SearchResultItem>> SearchVideoSummariesAsync(string[] queryTerms, SearchQuery query, CancellationToken ct)
    {
        var results = new List<SearchResultItem>();

        var summariesQuery = _dbContext.VideoSummaries
            .Include(s => s.Video)
            .Where(s => s.Video != null &&
                (s.Video.Status == VideoStatus.Published || s.Video.CreatedByOid == query.UserOid));

        if (query.VideoIdFilter.HasValue)
        {
            summariesQuery = summariesQuery.Where(s => s.VideoId == query.VideoIdFilter.Value);
        }

        var summaries = await summariesQuery.ToListAsync(ct);

        foreach (var summary in summaries)
        {
            if (summary.Video == null) continue;

            var summaryLower = (summary.Summary ?? "").ToLowerInvariant();
            var tldrLower = (summary.TlDr ?? "").ToLowerInvariant();
            var keywordsLower = string.Join(" ", summary.Keywords).ToLowerInvariant();
            var topicsLower = string.Join(" ", summary.Topics).ToLowerInvariant();

            var score = CalculateScore(summaryLower, queryTerms, 2.0f);
            score += CalculateScore(tldrLower, queryTerms, 2.5f);
            score += CalculateScore(keywordsLower, queryTerms, 2.0f);
            score += CalculateScore(topicsLower, queryTerms, 1.5f);

            if (score > 0)
            {
                var matchText = !string.IsNullOrEmpty(summary.TlDr) ? summary.TlDr :
                    !string.IsNullOrEmpty(summary.Summary) ? summary.Summary :
                    $"Keywords: {string.Join(", ", summary.Keywords)}";

                results.Add(new SearchResultItem
                {
                    VideoId = summary.VideoId,
                    VideoTitle = summary.Video.Title,
                    SegmentId = summary.Id,
                    StartMs = 0,
                    EndMs = 0,
                    Text = matchText,
                    Score = score,
                    Language = summary.Video.LanguageHint,
                    SourceType = "summary"
                });
            }
        }

        return results;
    }

    private async Task<List<SearchResultItem>> SearchVideoHighlightsAsync(string[] queryTerms, SearchQuery query, CancellationToken ct)
    {
        var results = new List<SearchResultItem>();

        var highlightsQuery = _dbContext.VideoHighlights
            .Include(h => h.Video)
            .Where(h => h.Video != null &&
                (h.Video.Status == VideoStatus.Published || h.Video.CreatedByOid == query.UserOid));

        if (query.VideoIdFilter.HasValue)
        {
            highlightsQuery = highlightsQuery.Where(h => h.VideoId == query.VideoIdFilter.Value);
        }

        var highlights = await highlightsQuery.ToListAsync(ct);

        foreach (var highlight in highlights)
        {
            if (highlight.Video == null) continue;

            var textLower = highlight.Text.ToLowerInvariant();
            var categoryLower = highlight.Category.ToLowerInvariant();

            var score = CalculateScore(textLower, queryTerms, 2.5f);
            score += CalculateScore(categoryLower, queryTerms, 0.5f);

            // Boost important highlights
            if (highlight.Importance.HasValue)
            {
                score *= (1 + highlight.Importance.Value);
            }

            if (score > 0)
            {
                results.Add(new SearchResultItem
                {
                    VideoId = highlight.VideoId,
                    VideoTitle = highlight.Video.Title,
                    SegmentId = highlight.Id,
                    StartMs = highlight.TimestampMs ?? 0,
                    EndMs = highlight.TimestampMs ?? 0,
                    Text = $"[{highlight.Category}] {highlight.Text}",
                    Score = score,
                    Language = highlight.Video.LanguageHint,
                    SourceType = "highlight"
                });
            }
        }

        return results;
    }

    private async Task<List<SearchResultItem>> SearchTranscriptSegmentsAsync(string[] queryTerms, SearchQuery query, CancellationToken ct)
    {
        var results = new List<SearchResultItem>();

        var segmentsQuery = _dbContext.TranscriptSegments
            .Include(s => s.Video)
            .Where(s => s.Video != null &&
                (s.Video.Status == VideoStatus.Published || s.Video.CreatedByOid == query.UserOid));

        if (query.VideoIdFilter.HasValue)
        {
            segmentsQuery = segmentsQuery.Where(s => s.VideoId == query.VideoIdFilter.Value);
        }

        if (!string.IsNullOrEmpty(query.LanguageFilter))
        {
            segmentsQuery = segmentsQuery.Where(s =>
                s.DetectedLanguage != null &&
                s.DetectedLanguage.ToLower() == query.LanguageFilter.ToLower());
        }

        var segments = await segmentsQuery.ToListAsync(ct);

        foreach (var segment in segments)
        {
            if (segment.Video == null) continue;

            var textLower = segment.Text.ToLowerInvariant();
            var score = CalculateScore(textLower, queryTerms, 1.0f);

            // Boost based on confidence
            if (segment.Confidence.HasValue)
            {
                score *= segment.Confidence.Value;
            }

            if (score > 0)
            {
                results.Add(new SearchResultItem
                {
                    VideoId = segment.VideoId,
                    VideoTitle = segment.Video.Title,
                    SegmentId = segment.Id,
                    StartMs = segment.StartMs,
                    EndMs = segment.EndMs,
                    Text = segment.Text,
                    Score = score,
                    Language = segment.DetectedLanguage,
                    SourceType = "transcript"
                });
            }
        }

        return results;
    }

    private static float CalculateScore(string text, string[] queryTerms, float weight)
    {
        if (string.IsNullOrEmpty(text) || queryTerms.Length == 0)
            return 0;

        float score = 0;
        foreach (var term in queryTerms)
        {
            if (text.Contains(term))
            {
                score += weight;

                // Boost for exact word match
                var words = text.Split(new[] { ' ', ',', '.', '!', '?', ';', ':', '-', '_', '(', ')', '[', ']' },
                    StringSplitOptions.RemoveEmptyEntries);
                if (words.Contains(term))
                {
                    score += weight * 0.5f;
                }

                // Boost for multiple occurrences
                var occurrences = text.Split(term).Length - 1;
                if (occurrences > 1)
                {
                    score += weight * 0.2f * Math.Min(occurrences - 1, 3);
                }
            }
        }

        return score / queryTerms.Length;
    }

    private static string HighlightText(string text, string[] queryTerms)
    {
        var result = text;
        foreach (var term in queryTerms)
        {
            var index = result.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            while (index >= 0)
            {
                var originalTerm = result.Substring(index, term.Length);
                // Avoid double-wrapping
                if (index < 4 || result.Substring(index - 4, 4) != "<em>")
                {
                    result = result.Substring(0, index) + $"<em>{originalTerm}</em>" + result.Substring(index + term.Length);
                    index = result.IndexOf(term, index + originalTerm.Length + 9, StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    index = result.IndexOf(term, index + term.Length, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
        return result;
    }

    private async Task<SearchFacets> BuildFacetsAsync(string userOid, string[] userGroupIds, CancellationToken ct)
    {
        // Get accessible videos for facets
        var videosQuery = _dbContext.VideoAssets
            .Where(v => v.Status == VideoStatus.Published || v.CreatedByOid == userOid);

        var videos = await videosQuery.ToListAsync(ct);

        var languages = videos
            .Where(v => !string.IsNullOrEmpty(v.LanguageHint))
            .GroupBy(v => v.LanguageHint!)
            .Select(g => new FacetValue(g.Key, g.Count()))
            .OrderByDescending(f => f.Count)
            .ToList();

        var videoFacets = videos
            .GroupBy(v => v.Title)
            .Select(g => new FacetValue(g.Key, g.Count()))
            .OrderByDescending(f => f.Count)
            .Take(10)
            .ToList();

        var segmentCount = await _dbContext.TranscriptSegments
            .Where(s => s.Video != null &&
                (s.Video.Status == VideoStatus.Published || s.Video.CreatedByOid == userOid))
            .CountAsync(ct);

        return new SearchFacets(
            Languages: languages,
            Videos: videoFacets,
            TotalVideos: videos.Count,
            TotalSegments: segmentCount
        );
    }

    #region ISearchIndexClient - Index operations (pass-through, not used for database search)

    public Task IndexSegmentAsync(SearchDocument document, CancellationToken ct = default)
    {
        // Not needed for database-backed search - data is stored in tables
        _logger.LogDebug("IndexSegmentAsync called but not needed for database search");
        return Task.CompletedTask;
    }

    public Task IndexSegmentsAsync(IEnumerable<SearchDocument> documents, CancellationToken ct = default)
    {
        // Not needed for database-backed search
        _logger.LogDebug("IndexSegmentsAsync called but not needed for database search");
        return Task.CompletedTask;
    }

    public Task DeleteVideoSegmentsAsync(Guid videoId, CancellationToken ct = default)
    {
        // Handled by database cascade delete
        _logger.LogDebug("DeleteVideoSegmentsAsync called but not needed for database search");
        return Task.CompletedTask;
    }

    public async Task<long> GetDocumentCountAsync(CancellationToken ct = default)
    {
        return await _dbContext.TranscriptSegments.CountAsync(ct);
    }

    public async Task<SearchFacets> GetFacetsAsync(string userOid, string[] userGroupIds, CancellationToken ct = default)
    {
        return await BuildFacetsAsync(userOid, userGroupIds, ct);
    }

    #endregion

    private class SearchResultItem
    {
        public Guid VideoId { get; set; }
        public string VideoTitle { get; set; } = "";
        public Guid? SegmentId { get; set; }
        public long StartMs { get; set; }
        public long EndMs { get; set; }
        public string Text { get; set; } = "";
        public float Score { get; set; }
        public string? Language { get; set; }
        public string SourceType { get; set; } = ""; // metadata, summary, highlight, transcript
    }
}
