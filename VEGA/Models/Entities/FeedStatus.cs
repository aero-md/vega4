namespace Models.Entities;

/// <summary>
/// Represents the current status of a feed.
/// Stored as int in DB for forward-compatible extensibility.
/// </summary>
public enum FeedStatus
{
    /// <summary>
    /// Feed is active and running normally
    /// </summary>
    Active = 0,

    /// <summary>
    /// Feed disabled because the Discord channel was deleted or is inaccessible
    /// </summary>
    ChannelDeleted = 1,

    /// <summary>
    /// Feed disabled because the subreddit is private, banned, non-existent, or quarantined
    /// </summary>
    TopicUnavailable = 2,

    /// <summary>
    /// Feed manually suspended (reserved for future admin/backoffice use)
    /// </summary>
    Suspended = 3,
}
