using System.Collections.Concurrent;
using System.Text.Json;
using Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Models.Business;
using Models.Entities;

namespace Services;

/// <summary>
/// Handles fetching Reddit content, caching it per subreddit,
/// and selecting the next post that hasn't been sent yet for a given feed.
/// </summary>
public class FeedContentService
{
    private const string REDDIT_POSTS_URL_TEMPLATE = "https://api.reddit.com/r/{0}/{1}.json?limit={2}";
    // `top` (and `controversial`) are time-windowed via Reddit's `t` param; we use the weekly top.
    private const string REDDIT_TOP_TIME_WINDOW = "week";
    private const string REDDIT_POST_URL_TEMPLATE = "https://reddit.com{0}";
    private const string REDDIT_IMAGE_URL_TEMPLATE = "https://i.redd.it/{0}.{1}";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FeedContentService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// In-memory cache of Reddit posts per subreddit (lowercase key).
    /// Each entry stores the fetched posts and the time they were fetched.
    /// </summary>
    private readonly ConcurrentDictionary<string, (List<RedditPostWrapper> Posts, DateTime FetchedAt)> _cache = new();

    public FeedContentService(
        IServiceScopeFactory scopeFactory,
        ILogger<FeedContentService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Reads the current feed configuration from the database.
    /// Called per tick — acceptable since ticks are spaced 15+ minutes apart.
    /// </summary>
    public async Task<FeedConfiguration> GetConfigAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        return await dbContext.FeedConfiguration.FirstAsync(c => c.Id == 1);
    }

    /// <summary>
    /// Saves updated configuration to the database.
    /// </summary>
    public async Task UpdateConfigAsync(FeedConfiguration config)
    {
        config.Validate();

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        dbContext.FeedConfiguration.Update(config);
        await dbContext.SaveChangesAsync();
    }


    #region Validation

    /// <summary>
    /// Validates a subreddit name and throws if invalid
    /// </summary>
    public static void ValidateSubreddit(string subreddit)
    {
        if (string.IsNullOrWhiteSpace(subreddit))
            throw new ArgumentException("Subreddit name cannot be empty");

        if (!FeedConfiguration.IsValidSubreddit(subreddit))
            throw new ArgumentException($"Invalid subreddit name: {subreddit}. Must be 2-21 alphanumeric characters or underscores.");
    }

    #endregion


    #region Cache management

    /// <summary>
    /// Refreshes the cache for a subreddit only if it's stale (older than FetchIntervalMinutes).
    /// </summary>
    public async Task RefreshCacheIfNeededAsync(string subreddit, FeedConfiguration config)
    {
        ValidateSubreddit(subreddit);
        var key = subreddit.ToLowerInvariant();

        if (_cache.TryGetValue(key, out var cached))
        {
            var elapsed = DateTime.UtcNow - cached.FetchedAt;
            if (elapsed.TotalMinutes < config.FetchIntervalMinutes)
                return; // Cache is still fresh
        }

        await FetchAndCachePostsAsync(subreddit, config);
    }

    /// <summary>
    /// Forces a cache refresh for a subreddit by fetching posts from Reddit.
    /// Throws HttpRequestException on non-success status codes (403, 404, etc.)
    /// so the caller can handle subreddit-level errors.
    /// </summary>
    public async Task FetchAndCachePostsAsync(string subreddit, FeedConfiguration config)
    {
        ValidateSubreddit(subreddit);
        var key = subreddit.ToLowerInvariant();

        var httpClient = _httpClientFactory.CreateClient(HttpClientNames.Reddit);
        var sort = config.SortMode.ToLowerInvariant();
        var url = string.Format(REDDIT_POSTS_URL_TEMPLATE, subreddit, sort, config.FetchSize);

        // `top` is time-windowed; without `t` Reddit defaults to all-time. We want the weekly top.
        if (sort == "top")
            url += $"&t={REDDIT_TOP_TIME_WINDOW}";

        var response = await httpClient.GetAsync(url);

        // Let 403/404 propagate as HttpRequestException with StatusCode
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Reddit API returned {StatusCode} for r/{Subreddit}", 
                (int)response.StatusCode, subreddit);
            response.EnsureSuccessStatusCode();
        }

        var json = await response.Content.ReadAsStringAsync();
        var redditResponse = JsonSerializer.Deserialize<RedditApiResponse>(json);

        if (redditResponse?.Data.Children != null)
        {
            _cache[key] = (redditResponse.Data.Children, DateTime.UtcNow);
            
            _logger.LogDebug("Cached {Count} posts for r/{Subreddit}", 
                redditResponse.Data.Children.Count, subreddit);
        }
    }

    #endregion


    #region Content selection

    /// <summary>
    /// Selects the next Reddit post for a feed that hasn't already been posted (not in history).
    /// Returns a tuple (formattedContent, postId) or null if no new content is available.
    /// </summary>
    public (string Content, string PostId)? GetNextPost(string subreddit, HashSet<string> historyIds, bool allowNsfw)
    {
        var key = subreddit.ToLowerInvariant();

        if (!_cache.TryGetValue(key, out var cached) || cached.Posts.Count == 0)
            return null;

        foreach (var post in cached.Posts)
        {
            // Skip if already in history
            if (historyIds.Contains(post.Data.Id))
                continue;

            // Skip NSFW content if not allowed
            if (post.Data.IsNsfw && !allowNsfw)
                continue;

            return (FormatPost(post), post.Data.Id);
        }

        return null;
    }

    #endregion


    #region Post formatting

    /// <summary>
    /// Formats a Reddit post into a Discord-ready message string.
    /// Handles galleries, hosted videos, and regular posts.
    /// </summary>
    private string FormatPost(RedditPostWrapper post)
    {
        var data = post.Data;

        // Gallery post (multiple images)
        if (data.IsGallery && data.MediaMetadata != null)
            return FormatGalleryPost(data);

        // Hosted video
        if (data.PostHint == "hosted:video")
            return string.Format(REDDIT_POST_URL_TEMPLATE, data.Permalink);

        // Regular post (image, link, etc.)
        return data.Url;
    }

    /// <summary>
    /// Formats a gallery post: shows the first image + a no-embed link to the full gallery.
    /// </summary>
    private string FormatGalleryPost(RedditPostData data)
    {
        try
        {
            var firstEntry = data.MediaMetadata!.First().Value;
            var mediaType = firstEntry.MimeType.Contains('/')
                ? firstEntry.MimeType.Split('/').Last()
                : firstEntry.MimeType;
            var count = data.MediaMetadata!.Count;
            var link = string.Format(REDDIT_IMAGE_URL_TEMPLATE, firstEntry.Id, mediaType);

            return $"({count} images) : <{data.Url}>\n{link}";
        }
        catch
        {
            return "Could not retrieve data from gallery: " + data.Url;
        }
    }

    #endregion
}