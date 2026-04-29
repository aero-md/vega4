using System.Text.RegularExpressions;

namespace Models.Core;

/// <summary>
/// Temporary configuration for feed content fetching.
/// This will eventually be replaced by per-feed DB-stored configuration.
/// </summary>
public partial class FeedContentConfiguration
{
    private static readonly string[] ValidSortModes = ["hot", "new", "top", "rising"];
    private static readonly Regex SubredditRegex = GenerateSubredditRegex();

    /// <summary>
    /// Number of posts to fetch from Reddit per subreddit (1-100)
    /// </summary>
    public int FetchSize { get; set; } = 70;

    /// <summary>
    /// How often (in minutes) to refresh the subreddit cache (minimum 1)
    /// </summary>
    public int FetchIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Max number of post IDs to remember per feed (to avoid re-posting).
    /// Oldest entries are discarded when this limit is exceeded.
    /// </summary>
    public int HistorySize { get; set; } = 60;

    /// <summary>
    /// Reddit sort mode used when fetching posts.
    /// Valid values: "hot", "new", "top", "rising"
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
    /// Validates a subreddit name format
    /// </summary>
    public static bool IsValidSubreddit(string subreddit)
    {
        return SubredditRegex.IsMatch(subreddit);
    }

    [GeneratedRegex(@"^[a-zA-Z0-9_]{2,21}$", RegexOptions.Compiled)]
    private static partial Regex GenerateSubredditRegex();
}
