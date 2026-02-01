using Microsoft.Extensions.Caching.Memory;

namespace T4L.VideoSearch.Api.Infrastructure.Caching;

/// <summary>
/// Cache service for frequently accessed data
/// </summary>
public interface ICacheService
{
    Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken ct = default);
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null);
    Task RemoveAsync(string key);
    Task RemoveByPrefixAsync(string prefix);
}

/// <summary>
/// In-memory cache service implementation
/// </summary>
public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly CacheSettings _settings;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly HashSet<string> _keys = [];
    private readonly ILogger<MemoryCacheService> _logger;

    public MemoryCacheService(
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<MemoryCacheService> logger)
    {
        _cache = cache;
        _settings = configuration.GetSection("Caching").Get<CacheSettings>() ?? new CacheSettings();
        _logger = logger;
    }

    public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(key, out T? cached))
        {
            _logger.LogDebug("Cache hit for {Key}", key);
            return cached;
        }

        await _semaphore.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (_cache.TryGetValue(key, out cached))
            {
                return cached;
            }

            _logger.LogDebug("Cache miss for {Key}, executing factory", key);
            var value = await factory();

            if (value != null)
            {
                var cacheExpiration = expiration ?? TimeSpan.FromSeconds(_settings.DefaultExpirationSeconds);
                var options = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(cacheExpiration)
                    .SetSlidingExpiration(TimeSpan.FromSeconds(_settings.SlidingExpirationSeconds))
                    .RegisterPostEvictionCallback((evictedKey, _, reason, _) =>
                    {
                        _keys.Remove(evictedKey.ToString()!);
                        _logger.LogDebug("Cache entry {Key} evicted: {Reason}", evictedKey, reason);
                    });

                _cache.Set(key, value, options);
                _keys.Add(key);
            }

            return value;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public Task<T?> GetAsync<T>(string key)
    {
        _cache.TryGetValue(key, out T? value);
        return Task.FromResult(value);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        var cacheExpiration = expiration ?? TimeSpan.FromSeconds(_settings.DefaultExpirationSeconds);
        var options = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(cacheExpiration)
            .SetSlidingExpiration(TimeSpan.FromSeconds(_settings.SlidingExpirationSeconds))
            .RegisterPostEvictionCallback((evictedKey, _, _, _) => _keys.Remove(evictedKey.ToString()!));

        _cache.Set(key, value, options);
        _keys.Add(key);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        _cache.Remove(key);
        _keys.Remove(key);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix)
    {
        var keysToRemove = _keys.Where(k => k.StartsWith(prefix)).ToList();
        foreach (var key in keysToRemove)
        {
            _cache.Remove(key);
            _keys.Remove(key);
        }
        _logger.LogDebug("Removed {Count} cache entries with prefix {Prefix}", keysToRemove.Count, prefix);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Cache key constants
/// </summary>
public static class CacheKeys
{
    public const string VideoListPrefix = "videos:list:";
    public const string VideoDetailPrefix = "videos:detail:";
    public const string SearchResultsPrefix = "search:results:";
    public const string SearchFacetsPrefix = "search:facets:";
    public const string UserPermissionsPrefix = "user:permissions:";
    public const string ModerationStatsPrefix = "moderation:stats:";

    public static string VideoList(string userId, int page, int pageSize) =>
        $"{VideoListPrefix}{userId}:{page}:{pageSize}";

    public static string VideoDetail(Guid videoId) =>
        $"{VideoDetailPrefix}{videoId}";

    public static string SearchResults(string queryHash) =>
        $"{SearchResultsPrefix}{queryHash}";

    public static string UserPermissions(string userId) =>
        $"{UserPermissionsPrefix}{userId}";

    public static string ModerationStats() =>
        $"{ModerationStatsPrefix}global";
}

/// <summary>
/// Cache settings
/// </summary>
public class CacheSettings
{
    public int DefaultExpirationSeconds { get; set; } = 300; // 5 minutes
    public int SlidingExpirationSeconds { get; set; } = 60; // 1 minute
    public int VideoListExpirationSeconds { get; set; } = 120; // 2 minutes
    public int SearchResultsExpirationSeconds { get; set; } = 180; // 3 minutes
    public int UserPermissionsExpirationSeconds { get; set; } = 300; // 5 minutes
}

/// <summary>
/// Response caching configuration
/// </summary>
public static class ResponseCachingConfiguration
{
    public static IServiceCollection AddResponseCachingConfiguration(this IServiceCollection services)
    {
        services.AddResponseCaching(options =>
        {
            options.MaximumBodySize = 64 * 1024 * 1024; // 64 MB max cached response
            options.UseCaseSensitivePaths = false;
        });

        services.AddOutputCache(options =>
        {
            // Default policy - no caching
            options.AddBasePolicy(builder => builder.NoCache());

            // Search suggestions - cache for 1 minute
            options.AddPolicy("SearchSuggestions", builder =>
            {
                builder.Cache()
                    .Expire(TimeSpan.FromMinutes(1))
                    .SetVaryByQuery("q")
                    .Tag("search");
            });

            // Video list - cache for 2 minutes, vary by user
            options.AddPolicy("VideoList", builder =>
            {
                builder.Cache()
                    .Expire(TimeSpan.FromMinutes(2))
                    .SetVaryByQuery("page", "pageSize", "status")
                    .SetVaryByHeader("X-Dev-User-Id", "Authorization")
                    .Tag("videos");
            });

            // Search facets - cache for 5 minutes
            options.AddPolicy("SearchFacets", builder =>
            {
                builder.Cache()
                    .Expire(TimeSpan.FromMinutes(5))
                    .Tag("search");
            });

            // Static content - cache for 1 hour
            options.AddPolicy("StaticContent", builder =>
            {
                builder.Cache()
                    .Expire(TimeSpan.FromHours(1))
                    .Tag("static");
            });
        });

        return services;
    }
}

/// <summary>
/// Cache invalidation service for coordinating cache updates
/// </summary>
public interface ICacheInvalidationService
{
    Task InvalidateVideoAsync(Guid videoId);
    Task InvalidateVideoListAsync();
    Task InvalidateSearchAsync();
    Task InvalidateUserPermissionsAsync(string userId);
    Task InvalidateAllAsync();
}

public class CacheInvalidationService : ICacheInvalidationService
{
    private readonly ICacheService _cache;
    private readonly ILogger<CacheInvalidationService> _logger;

    public CacheInvalidationService(ICacheService cache, ILogger<CacheInvalidationService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task InvalidateVideoAsync(Guid videoId)
    {
        await _cache.RemoveAsync(CacheKeys.VideoDetail(videoId));
        await _cache.RemoveByPrefixAsync(CacheKeys.VideoListPrefix);
        _logger.LogInformation("Invalidated cache for video {VideoId}", videoId);
    }

    public async Task InvalidateVideoListAsync()
    {
        await _cache.RemoveByPrefixAsync(CacheKeys.VideoListPrefix);
        _logger.LogInformation("Invalidated video list cache");
    }

    public async Task InvalidateSearchAsync()
    {
        await _cache.RemoveByPrefixAsync(CacheKeys.SearchResultsPrefix);
        await _cache.RemoveByPrefixAsync(CacheKeys.SearchFacetsPrefix);
        _logger.LogInformation("Invalidated search cache");
    }

    public async Task InvalidateUserPermissionsAsync(string userId)
    {
        await _cache.RemoveAsync(CacheKeys.UserPermissions(userId));
        _logger.LogInformation("Invalidated permissions cache for user {UserId}", userId);
    }

    public async Task InvalidateAllAsync()
    {
        await _cache.RemoveByPrefixAsync("");
        _logger.LogInformation("Invalidated all caches");
    }
}
