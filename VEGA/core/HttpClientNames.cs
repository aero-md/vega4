namespace Core;

public static class HttpClientNames
{
    public const string Reddit = "Reddit";
    public const string AnimeImages = "AnimeImages";
    public const string Emotes = "Emotes";

    /// <summary>
    /// Per-response in-memory buffer cap for emote downloads.
    /// Discord caps emoji files at 256 KB; 8 MB leaves headroom for animated GIFs
    /// while bounding RAM usage if the CDN ever returns something unexpected.
    /// </summary>
    public const long EmoteMaxResponseBytes = 8 * 1024 * 1024;
}
