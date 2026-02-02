using System.Text.Json;
using Core;
using Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Models.Entities;
using NetCord.JsonModels;

namespace Services;

public class GuildSettingsService
{
    // Consts related to cache. TODO : move somes of these to config
    private const int CACHE_LIFETIME_IN_MINUTES = 10;
    private const int MAX_TRIGGER_COUNT_BY_GUID = 10;
    private const string CACHE_PREFIX = "guildSettings_";

    private readonly AppDbContext _dbContext;
    private readonly IMemoryCache _cache;

    // Getter to normalize cache key structure
    private string GetCacheKey(ulong guildId) => CACHE_PREFIX + guildId; 

    public GuildSettingsService(AppDbContext dbContext, IMemoryCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }


    /// <summary>
    /// Get a GuildSettings object. Attempts to retrieve it from cache, if not found in cache,
    /// fetch it in DB. If not in DB either, return a new GuildSettings object with default values.
    /// </summary>
    public async Task<GuildSettings> GetByIdAsync(ulong guildId)
    {
        var cacheKey = GetCacheKey(guildId);

        // Found in cache
        if (_cache.TryGetValue(cacheKey, out GuildSettings? cachedSettings))
            return cachedSettings!;

        var dbSettings = await _dbContext.GuildSettings
                                    .Include(g => g.Triggers) // Eagerly load Triggers
                                    .FirstOrDefaultAsync(g => g.GuildId == guildId);
        // Found in BDD
        if (dbSettings != null)
        {
            // Add to cache and return cache instance
            _cache.Set(cacheKey, dbSettings, new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(CACHE_LIFETIME_IN_MINUTES)));

            return dbSettings;
        }
        // Found in neither BDD nor cache
        else
        {
            // New object with default values
            return new GuildSettings(guildId);
        }
    }


    /// <summary>
    /// SaveOrUpdate a GuildSettings object in cache and DB
    /// </summary>
    /// <param name="guildId"></param>
    /// <param name="newSettings"></param>
    /// <returns></returns>
    /// <exception cref="BusinessException"></exception>
    public async Task<GuildSettings> SaveOrUpdateAsync(ulong guildId, GuildSettings newSettings)
    {
        // Second level validation -> should have been checked a first time in business code
        if (guildId != newSettings.GuildId)
            throw new SlashCommandBusinessException("GuildId and GuildSettings ID mismatch");

        GuildSettings? existingSettings = _dbContext.GuildSettings.Find(guildId);
        if (existingSettings is null)
        {
            _dbContext.GuildSettings.Add(newSettings);
        }
        else
        {
            _dbContext.Entry(existingSettings).CurrentValues.SetValues(newSettings);
            // Handle triggers separately if needed
        }
        
        // If oject does exist its changes are already being tracked
        await _dbContext.SaveChangesAsync();

        // Update cache
        _cache.Set(GetCacheKey(guildId), newSettings);

        return newSettings;
    }


    /// <summary>
    /// Add a trigger on the targeted GuildSettings
    /// </summary>
    /// <param name="guildId"></param>
    /// <param name="trigger"></param>
    /// <returns></returns>
    public async Task<GuildSettings> AddTrigger(ulong guildId, Trigger trigger)
    {
        GuildSettings settings = await GetByIdAsync(guildId);

        if (settings.Triggers.Count >= MAX_TRIGGER_COUNT_BY_GUID)
            throw new SlashCommandBusinessException($"You can't create more than {MAX_TRIGGER_COUNT_BY_GUID} triggers for each server");

        settings.Triggers.Add(trigger);

        GuildSettings updatedSettings = await SaveOrUpdateAsync(guildId, settings);
        return updatedSettings;
    }


    /// <summary>
    /// Delete a trigger on the targeted GuildSettings. Returns the deleted trigger's pattern.
    /// </summary>
    /// <param name="guildId"></param>
    /// <param name="trigger"></param>
    /// <returns></returns>
    public async Task<string?> DeleteTrigger(ulong guildId, int triggerIndex)
    {
        GuildSettings settings = await GetByIdAsync(guildId);

        if (settings.Triggers.Count == 0)
            throw new SlashCommandBusinessException($"There are no triggers on this server");
        
        Trigger trigger = settings.Triggers.OrderByDescending(x => x.CreatedAt)
                                           .ToList()
                                           .ElementAt(triggerIndex);

        Guid? id = trigger?.TriggerId;
        string? pattern = trigger?.Pattern;
        
        if (id == null)
            return null;

        settings.Triggers.RemoveAll(x => x.TriggerId == id);
        GuildSettings updatedSettings = await SaveOrUpdateAsync(guildId, settings);

        return pattern;
    }
}