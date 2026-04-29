using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Models.Entities;

/// <summary>
/// Stores feed system configuration in the database.
/// Single-row table (Key = 1) — acts as a global settings store.
/// All values can be updated at runtime via backoffice commands.
/// </summary>
public partial class FeedConfiguration
{
    private static readonly string[] ValidSortModes = ["hot", "new", "top", "rising"];
    private static readonly Regex SubredditRegex = GenerateSubredditRegex();

    /// <summary>
    /// Single-row key, always 1
    /// </summary>
    [Key]
    public int Id { get; set; } = 1;

    /// <summary>
    /// Number of posts to fetch from Reddit per subreddit (1-100)
    /// </summary>
    public int FetchSize { get; set; } = 70;

    /// <summary>
    /// How often (in minutes) to refresh the subreddit cache (minimum 1)
    /// </summary>
    public int FetchIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Max number of post IDs to remember per feed (to avoid re-posting)
    /// </summary>
    public int HistorySize { get; set; } = 60;

    /// <summary>
    /// Reddit sort mode used when fetching posts ("hot", "new", "top", "rising")
    /// </summary>
    public string SortMode { get; set; } = "hot";

    /// <summary>
    /// Maximum number of feeds allowed per guild
    /// </summary>
    public int MaxFeedsPerGuild { get; set; } = 5;

    /// <summary>
    /// Validates the configuration and throws if invalid
    /// </summary>
    public void Validate()
    {
        if (FetchSize < 1 || FetchSize > 100)
            throw new ArgumentException($"FetchSize must be between 1 and 100, got {FetchSize}");

        if (FetchIntervalMinutes < 1)
            throw new ArgumentException($"FetchIntervalMinutes must be at least 1, got {FetchIntervalMinutes}");

        if (HistorySize < 1)
            throw new ArgumentException($"HistorySize must be at least 1, got {HistorySize}");

        if (!ValidSortModes.Contains(SortMode.ToLowerInvariant()))
            throw new ArgumentException($"SortMode must be one of: {string.Join(", ", ValidSortModes)}, got {SortMode}");

        if (MaxFeedsPerGuild < 1)
            throw new ArgumentException($"MaxFeedsPerGuild must be at least 1, got {MaxFeedsPerGuild}");
    }

    /// <summary>
    /// Validates a subreddit name format (2-21 alphanumeric/underscore characters)
    /// </summary>
    public static bool IsValidSubreddit(string subreddit)
    {
        return SubredditRegex.IsMatch(subreddit);
    }

    /// <summary>
    /// Returns the list of valid sort modes for display/validation
    /// </summary>
    public static string[] GetValidSortModes() => ValidSortModes;

    [GeneratedRegex(@"^[a-zA-Z0-9_]{2,21}$", RegexOptions.Compiled)]
    private static partial Regex GenerateSubredditRegex();
}
