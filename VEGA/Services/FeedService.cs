using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Tasks;
using Core;
using Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Models.Entities;

namespace Services;

public class FeedService
{
    private readonly AppDbContext _dbContext;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _timersCancellationTokens = new();
    
    public FeedService(AppDbContext dbContext)
    {
        _dbContext = dbContext;

        // On service initialization, load all existing feeds from DB and initiate their timers
        var existingFeeds = _dbContext.FeedProperties.ToListAsync().Result;

        foreach (var feed in existingFeeds)
        {
            CancellationTokenSource ccts = new();
            
            RegisterTimer(feed);
        }
    }

    #region Feed Management methods

    /// <summary>
    /// Adds a new feed to DB and initiates its recurring timer
    /// </summary>
    public async Task CreateNewFeedAsync(FeedProperties feedProperties)
    {
        // Add feedProperties to DB
        _dbContext.FeedProperties.Add(feedProperties);
        await _dbContext.SaveChangesAsync();

        RegisterTimer(feedProperties);
    }


    /// <summary>
    /// Removes a feed from DB and cancels its recurring timer
    /// </summary>
    /// <param name="feedId"></param>
    /// <returns></returns>
    public async Task RemoveFeedAsync(ulong guildId, int feedIndex)
    {
        FeedProperties? feed = 
            await _dbContext.FeedProperties.Where(f => f.GuildId == guildId)
                                           .OrderByDescending(f => f.CreatedAt)
                                           .Skip(feedIndex)
                                           .FirstOrDefaultAsync();

        if (feed != null)
        {
            _dbContext.FeedProperties.Remove(feed);
            await _dbContext.SaveChangesAsync();

            if (_timersCancellationTokens.TryRemove(feed.FeedId, out var ccts))
            {
                ccts.Cancel();
                ccts.Dispose();
            }
        }
        else
        {
            throw new SlashCommandBusinessException("Feed not found for given guildId and ID");
        }
    }


    /// <summary>
    /// Returns a list of active feeds for a given guildId
    /// </summary>
    /// <param name="guildId"></param>
    /// <returns></returns>
    public async Task<List<FeedProperties>> GetActiveFeedsAsync(ulong guildId)
    {
        List<FeedProperties> feeds = 
            await _dbContext.FeedProperties
                            .Where(f => f.GuildId == guildId)
                            .ToListAsync();

        return feeds;
    }

    #endregion


    #region Feed history methods

    /// <summary>
    /// Returns a list of FeedHistoryPost linked to a given feedId
    /// </summary>
    public async Task<List<FeedPostReceit>> GetFeedHistoryAsync(Guid feedId)
    {
        List<FeedPostReceit> postList = 
            await _dbContext.FeedHistory
                            .Where(p => p.FeedId == feedId)
                            .ToListAsync();

        return postList;
    }

    /// <summary>
    /// Returns true/false depending on success of saving a postId in FeedHistoryPost for a given feedId
    /// </summary>
    public async Task<bool> AddPostToFeedHistoryAsync(Guid feedId, string postId)
    {
        List<FeedPostReceit> history = 
            await _dbContext.FeedHistory.Where(p => p.FeedId == feedId)
                                        .ToListAsync();

        // Clear older items exceding max capacity
        if(history.Count > 99)
        {
            var itemsToDelete = history.OrderBy(x => x.PostedAt)
                                       .Skip(99)
                                       .ToList();

            _dbContext.FeedHistory.RemoveRange(itemsToDelete);
        }

        // Create new item
        var newItem = new FeedPostReceit
        {
            FeedId = feedId,
            PostId = postId,
            PostedAt = DateTime.UtcNow
        };
        // Add new item to context
        await _dbContext.FeedHistory.AddAsync(newItem);

        //Save changes to DB
        var result = await _dbContext.SaveChangesAsync();

        return result > 0;
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
            (key, existingValue) => ccts
        );
        
        _ = LaunchTimer(feedProperties, ccts);
    }

    /// <summary>
    /// Initiates a recurring timer for a given feed
    /// </summary>
    /// <param name="feedProperties"></param>
    /// <param name="ccts"></param>
    /// <returns></returns>
    private async Task LaunchTimer(FeedProperties feedProperties, CancellationTokenSource ccts)
    {
        PeriodicTimer timer = new(TimeSpan.FromMinutes(feedProperties.IntervalInMinutes));

        try
        {
            while (await timer.WaitForNextTickAsync(ccts.Token))
            {
                // TODO : Implement feed fetching and posting logic here

                Console.WriteLine("Hello World after 10 seconds");
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Timer cancelled");
        }
    }
    
    #endregion
}