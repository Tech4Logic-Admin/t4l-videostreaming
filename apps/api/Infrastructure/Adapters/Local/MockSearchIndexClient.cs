using System.Collections.Concurrent;

namespace T4L.VideoSearch.Api.Infrastructure.Adapters.Local;

/// <summary>
/// Mock search index client for local development - uses in-memory storage
/// </summary>
public class MockSearchIndexClient : ISearchIndexClient
{
    private readonly ILogger<MockSearchIndexClient> _logger;
    private readonly ConcurrentDictionary<string, SearchDocument> _documents = new();

    public MockSearchIndexClient(ILogger<MockSearchIndexClient> logger)
    {
        _logger = logger;
    }

    public Task IndexSegmentAsync(SearchDocument document, CancellationToken ct = default)
    {
        _documents[document.Id] = document;
        _logger.LogDebug("Mock Search: Indexed segment {Id} for video {VideoId}", document.Id, document.VideoId);
        return Task.CompletedTask;
    }

    public Task IndexSegmentsAsync(IEnumerable<SearchDocument> documents, CancellationToken ct = default)
    {
        var count = 0;
        foreach (var doc in documents)
        {
            _documents[doc.Id] = doc;
            count++;
        }
        _logger.LogInformation("Mock Search: Indexed {Count} segments", count);
        return Task.CompletedTask;
    }

    public Task<SearchResults> SearchAsync(SearchQuery query, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var queryLower = query.QueryText.ToLowerInvariant();
        var queryTerms = queryLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var filteredDocs = _documents.Values
            // Security trimming: only return documents the user has access to
            .Where(d =>
                d.AllowedUserOids.Contains(query.UserOid) ||
                d.AllowedGroupIds.Any(g => query.UserGroupIds.Contains(g)) ||
                (d.AllowedUserOids.Length == 0 && d.AllowedGroupIds.Length == 0)); // Public

        // Apply language filter
        if (!string.IsNullOrEmpty(query.LanguageFilter))
        {
            filteredDocs = filteredDocs.Where(d =>
                d.Language != null &&
                d.Language.Equals(query.LanguageFilter, StringComparison.OrdinalIgnoreCase));
        }

        // Apply video filter
        if (query.VideoIdFilter.HasValue)
        {
            filteredDocs = filteredDocs.Where(d => d.VideoId == query.VideoIdFilter.Value);
        }

        // Calculate scores and filter
        var scoredDocs = filteredDocs
            .Select(d => new
            {
                Document = d,
                Score = CalculateScore(d.Text, d.VideoTitle, queryTerms)
            })
            .Where(x => x.Score > 0);

        // Apply sorting
        var sortedDocs = query.SortOrder switch
        {
            SearchSortOrder.TitleAsc => scoredDocs.OrderBy(x => x.Document.VideoTitle),
            SearchSortOrder.TitleDesc => scoredDocs.OrderByDescending(x => x.Document.VideoTitle),
            SearchSortOrder.DateAsc => scoredDocs.OrderBy(x => x.Document.StartMs),
            SearchSortOrder.DateDesc => scoredDocs.OrderByDescending(x => x.Document.StartMs),
            _ => scoredDocs.OrderByDescending(x => x.Score) // Relevance
        };

        var totalCount = sortedDocs.Count();

        var hits = sortedDocs
            .Skip(query.Skip)
            .Take(query.Take)
            .Select(x => new SearchHit(
                VideoId: x.Document.VideoId,
                VideoTitle: x.Document.VideoTitle,
                SegmentId: Guid.Parse(x.Document.Id),
                StartMs: x.Document.StartMs,
                EndMs: x.Document.EndMs,
                Text: x.Document.Text,
                HighlightedText: HighlightText(x.Document.Text, queryTerms),
                Score: x.Score,
                Language: x.Document.Language
            ))
            .ToList();

        sw.Stop();

        _logger.LogInformation("Mock Search: Query '{Query}' returned {Count} results in {Ms}ms",
            query.QueryText, hits.Count, sw.ElapsedMilliseconds);

        // Build facets
        var facets = BuildFacets(filteredDocs.ToList());

        return Task.FromResult(new SearchResults(
            Hits: hits,
            TotalCount: totalCount,
            LatencyMs: sw.ElapsedMilliseconds,
            Facets: facets
        ));
    }

    public Task<SearchFacets> GetFacetsAsync(string userOid, string[] userGroupIds, CancellationToken ct = default)
    {
        var accessibleDocs = _documents.Values
            .Where(d =>
                d.AllowedUserOids.Contains(userOid) ||
                d.AllowedGroupIds.Any(g => userGroupIds.Contains(g)) ||
                (d.AllowedUserOids.Length == 0 && d.AllowedGroupIds.Length == 0))
            .ToList();

        return Task.FromResult(BuildFacets(accessibleDocs));
    }

    public Task DeleteVideoSegmentsAsync(Guid videoId, CancellationToken ct = default)
    {
        var keysToRemove = _documents
            .Where(kvp => kvp.Value.VideoId == videoId)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _documents.TryRemove(key, out _);
        }

        _logger.LogInformation("Mock Search: Deleted {Count} segments for video {VideoId}", keysToRemove.Count, videoId);
        return Task.CompletedTask;
    }

    public Task<long> GetDocumentCountAsync(CancellationToken ct = default)
    {
        return Task.FromResult((long)_documents.Count);
    }

    private static SearchFacets BuildFacets(List<SearchDocument> documents)
    {
        var languages = documents
            .Where(d => !string.IsNullOrEmpty(d.Language))
            .GroupBy(d => d.Language!)
            .Select(g => new FacetValue(g.Key, g.Count()))
            .OrderByDescending(f => f.Count)
            .ToList();

        var videos = documents
            .GroupBy(d => d.VideoTitle)
            .Select(g => new FacetValue(g.Key, g.Count()))
            .OrderByDescending(f => f.Count)
            .Take(10)
            .ToList();

        return new SearchFacets(
            Languages: languages,
            Videos: videos,
            TotalVideos: documents.Select(d => d.VideoId).Distinct().Count(),
            TotalSegments: documents.Count
        );
    }

    private static float CalculateScore(string text, string title, string[] queryTerms)
    {
        var textLower = text.ToLowerInvariant();
        var titleLower = title.ToLowerInvariant();

        float score = 0;
        foreach (var term in queryTerms)
        {
            // Text match
            if (textLower.Contains(term))
            {
                score += 1.0f;
                // Boost for exact word match
                if (textLower.Split(' ').Contains(term))
                    score += 0.5f;
            }
            // Title match (higher weight)
            if (titleLower.Contains(term))
            {
                score += 2.0f;
            }
        }

        return queryTerms.Length > 0 ? score / queryTerms.Length : 0f;
    }

    private static string HighlightText(string text, string[] queryTerms)
    {
        var result = text;
        foreach (var term in queryTerms)
        {
            // Case-insensitive highlight with <em> tags
            var index = result.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                var originalTerm = result.Substring(index, term.Length);
                result = result.Replace(originalTerm, $"<em>{originalTerm}</em>");
            }
        }
        return result;
    }
}
