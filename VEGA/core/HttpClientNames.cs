namespace Core;

public static class HttpClientNames
{
    public const string Reddit = "Reddit";
    public const string AnimeImages = "AnimeImages";
    public const string Emotes = "Emotes";

    /// <summary>
    /// User-Agent sent to the Reddit API. Reddit returns 403 for generic/empty UAs,
    /// so we masquerade as a regular Firefox desktop browser to blend in with normal
    /// traffic rather than advertising a bot.
    /// </summary>
    public const string RedditUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:134.0) Gecko/20100101 Firefox/134.0";

    /// <summary>
    /// Per-response in-memory buffer cap for emote downloads.
    /// Discord caps emoji files at 256 KB; 8 MB leaves headroom for animated GIFs
    /// while bounding RAM usage if the CDN ever returns something unexpected.
    /// </summary>
    public const long EmoteMaxResponseBytes = 8 * 1024 * 1024;
}
