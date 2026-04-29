using System.Collections.Concurrent;
using Core;
using Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Resources;
using Models.Entities;
using NetCord;
using NetCord.Rest;

namespace Services;

public class FeedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FeedContentService _contentService;
    private readonly ILogger<FeedService> _logger;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _timersCancellationTokens = new();
    private RestClient _restClient = null!;

    public FeedService(
        IServiceScopeFactory scopeFactory, 
        FeedContentService contentService, 
        ILogger<FeedService> logger)
    {
        _scopeFactory = scopeFactory;
        _contentService = contentService;
        _logger = logger;
    }

    /// <summary>
    /// Initializes the service with the RestClient (must be called after Vega.Initialize())
    /// and loads existing feeds from DB to start their timers.
    /// </summary>
    public async Task Initialize(RestClient restClient)
    {
        _restClient = restClient;

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var existingFeeds = await dbContext.FeedProperties
            .Where(f => f.Status == FeedStatus.Active)
            .ToListAsync();

        foreach (var feed in existingFeeds)
        {
            RegisterTimer(feed);
        }

        _logger.LogInformation("FeedService initialized with {FeedCount} active feed(s)", existingFeeds.Count);
    }


    #region Feed Management methods

    /// <summary>
    /// Adds a new feed to DB and initiates its recurring timer.
    /// Validates subreddit name and enforces guild feed limit.
    /// </summary>
    public async Task CreateNewFeedAsync(FeedProperties feedProperties)
    {
        // Validate subreddit name
        FeedContentService.ValidateSubreddit(feedProperties.Topic);

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Read config from DB for current limit
        var config = await _contentService.GetConfigAsync();

        // Check feed limit per guild
        var currentFeedCount = await dbContext.FeedProperties
            .CountAsync(f => f.GuildId == feedProperties.GuildId && f.Status == FeedStatus.Active);

        if (currentFeedCount >= config.MaxFeedsPerGuild)
        {
            throw new SlashCommandBusinessException(
                string.Format(Strings.Exceptions.FeedLimitReached, config.MaxFeedsPerGuild));
        }

        dbContext.FeedProperties.Add(feedProperties);
        await dbContext.SaveChangesAsync();

        RegisterTimer(feedProperties);
        
        _logger.LogInformation("Created feed {FeedId} for r/{Subreddit} in guild {GuildId}", 
            feedProperties.FeedId, feedProperties.Topic, feedProperties.GuildId);
    }

    /// <summary>
    /// Removes a feed from DB by its UUID and cancels its recurring timer
    /// </summary>
    public async Task RemoveFeedAsync(ulong guildId, Guid feedId)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var feed = await dbContext.FeedProperties
            .FirstOrDefaultAsync(f => f.GuildId == guildId && f.FeedId == feedId);

        if (feed != null)
        {
            dbContext.FeedProperties.Remove(feed);
            await dbContext.SaveChangesAsync();

            if (_timersCancellationTokens.TryRemove(feed.FeedId, out var ccts))
            {
                await ccts.CancelAsync();
                ccts.Dispose();
            }
            
            _logger.LogInformation("Removed feed {FeedId} for r/{Subreddit} from guild {GuildId}", 
                feedId, feed.Topic, guildId);
        }
        else
        {
            throw new SlashCommandBusinessException(Strings.Exceptions.FeedNotFound);
        }
    }

    /// <summary>
    /// Returns all feeds for a given guildId (including inactive ones, for display)
    /// </summary>
    public async Task<List<FeedProperties>> GetFeedsAsync(ulong guildId)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await dbContext.FeedProperties
            .Where(f => f.GuildId == guildId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Sets a feed's status to the given value and cancels its timer.
    /// Used when a feed must be disabled but not deleted (channel gone, subreddit unavailable, etc.)
    /// </summary>
    private async Task DisableFeedAsync(Guid feedId, FeedStatus reason)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var feed = await dbContext.FeedProperties.FindAsync(feedId);
        if (feed != null)
        {
            feed.Status = reason;
            await dbContext.SaveChangesAsync();

            // Cancel and remove the timer
            if (_timersCancellationTokens.TryRemove(feedId, out var ccts))
            {
                await ccts.CancelAsync();
                ccts.Dispose();
            }
            
            _logger.LogWarning("Disabled feed {FeedId} for r/{Subreddit} — reason: {Reason}", 
                feedId, feed.Topic, reason);
        }
    }

    #endregion


    #region Feed history methods

    /// <summary>
    /// Returns only the PostIds from history for a given feedId (optimized query)
    /// </summary>
    private async Task<HashSet<string>> GetFeedHistoryIdsAsync(Guid feedId)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var postIds = await dbContext.FeedHistory
            .Where(p => p.FeedId == feedId)
            .Select(p => p.PostId)
            .ToListAsync();

        return postIds.ToHashSet();
    }

    /// <summary>
    /// Saves a postId to the feed's history, trimming old entries beyond history size limit.
    /// </summary>
    private async Task AddPostToFeedHistoryAsync(Guid feedId, string postId)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Count current history size
        var historyCount = await dbContext.FeedHistory
            .CountAsync(p => p.FeedId == feedId);

        // Clear older items exceeding max history size
        var config = await _contentService.GetConfigAsync();
        int maxHistory = config.HistorySize;
        if (historyCount >= maxHistory)
        {
            var itemsToDelete = await dbContext.FeedHistory
                .Where(p => p.FeedId == feedId)
                .OrderBy(x => x.PostedAt)
                .Take(historyCount - maxHistory + 1)
                .ToListAsync();

            dbContext.FeedHistory.RemoveRange(itemsToDelete);
        }

        // Create new history entry
        var newItem = new FeedPostReceit
        {
            FeedId = feedId,
            PostId = postId,
            PostedAt = DateTime.UtcNow
        };

        await dbContext.FeedHistory.AddAsync(newItem);
        await dbContext.SaveChangesAsync();
    }

    #endregion


    #region Timer management methods

    private void RegisterTimer(FeedProperties feedProperties)
    {
        CancellationTokenSource ccts = new();

        _timersCancellationTokens.AddOrUpdate
        (
            feedProperties.FeedId,
            ccts,
            (key, existingValue) =>
            {
                existingValue.Dispose();
                return ccts;
            }
        );

        // Use Task.Run to ensure exceptions are properly observed
        _ = Task.Run(async () =>
        {
            try
            {
                await LaunchTimerAsync(feedProperties, ccts.Token);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unexpected error in feed timer for {FeedId} (r/{Subreddit})", 
                    feedProperties.FeedId, feedProperties.Topic);
            }
        });
    }

    /// <summary>
    /// Initiates a recurring timer for a given feed.
    /// Implements StartAtMinute alignment and proper disposal.
    /// </summary>
    private async Task LaunchTimerAsync(FeedProperties feedProperties, CancellationToken cancellationToken)
    {
        // Wait for start minute alignment if specified
        if (feedProperties.StartAtMinute >= 0 && feedProperties.StartAtMinute < 60)
        {
            var now = DateTime.UtcNow;
            var targetMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, feedProperties.StartAtMinute, 0, DateTimeKind.Utc);
            
            if (targetMinute <= now)
                targetMinute = targetMinute.AddHours(1);
            
            var delay = targetMinute - now;
            _logger.LogInformation("Feed {FeedId} (r/{Subreddit}) waiting {Delay} until minute {Minute}", 
                feedProperties.FeedId, feedProperties.Topic, delay, feedProperties.StartAtMinute);
            
            await Task.Delay(delay, cancellationToken);
        }

        // Use 'using' to properly dispose the timer
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(feedProperties.IntervalInMinutes));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await ProcessFeedTickAsync(feedProperties);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Feed timer stopped for {FeedId} (r/{Subreddit})", 
                feedProperties.FeedId, feedProperties.Topic);
        }
    }

    /// <summary>
    /// Processes a single tick for a feed:
    /// 1. Refreshes the subreddit cache if stale
    /// 2. Picks the next post not in this feed's history
    /// 3. Sends it to the Discord channel
    /// 4. Records it in history
    /// </summary>
    private async Task ProcessFeedTickAsync(FeedProperties feed)
    {
        try
        {
            // 0. Read current config from DB
            var config = await _contentService.GetConfigAsync();

            // 1. Refresh Reddit cache for this subreddit if needed
            await _contentService.RefreshCacheIfNeededAsync(feed.Topic, config);

            // 2. Get this feed's post history (optimized: only IDs)
            var historyIds = await GetFeedHistoryIdsAsync(feed.FeedId);

            // 3. Pick next post not already sent (with NSFW filter)
            var nextPost = _contentService.GetNextPost(feed.Topic, historyIds, feed.AllowNsfw);

            if (nextPost == null)
            {
                // No new content available - silent, not an error
                return;
            }

            var (content, postId) = nextPost.Value;

            // 4. Send message to the Discord channel
            try
            {
                var channel = await _restClient.GetChannelAsync(feed.ChannelId);

                if (channel is TextChannel textChannel)
                {
                    await textChannel.SendMessageAsync(new MessageProperties
                    {
                        Content = content
                    });

                    _logger.LogInformation("Posted {PostId} to channel {ChannelId} for r/{Subreddit}", 
                        postId, feed.ChannelId, feed.Topic);
                }
                else
                {
                    _logger.LogWarning("Channel {ChannelId} is not a text channel, skipping feed r/{Subreddit}", 
                        feed.ChannelId, feed.Topic);
                    return;
                }
            }
            catch (RestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Channel was deleted - disable the feed
                _logger.LogWarning("Channel {ChannelId} not found (deleted?), disabling feed {FeedId}",
                    feed.ChannelId, feed.FeedId);

                await DisableFeedAsync(feed.FeedId, FeedStatus.ChannelDeleted);
                return;
            }

            // 5. Save post to history
            await AddPostToFeedHistoryAsync(feed.FeedId, postId);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Forbidden 
                                                             or System.Net.HttpStatusCode.NotFound)
        {
            // Subreddit is private (403), banned, or doesn't exist (404) 
            _logger.LogWarning("Reddit returned {StatusCode} for r/{Subreddit}, disabling feed {FeedId}",
                ex.StatusCode, feed.Topic, feed.FeedId);
            
            await DisableFeedAsync(feed.FeedId, FeedStatus.TopicUnavailable);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.TooManyRequests)
        {
            // Rate limited by Reddit - skip this tick, try again next interval
            _logger.LogWarning("Reddit rate limit hit for r/{Subreddit}, skipping tick", feed.Topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing feed for r/{Subreddit} → channel {ChannelId}", 
                feed.Topic, feed.ChannelId);
        }
    }

    #endregion
}