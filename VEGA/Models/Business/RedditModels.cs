using System.Text.Json.Serialization;

namespace Models.Business;

public class RedditApiResponse
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "";

    [JsonPropertyName("data")]
    public RedditListingData Data { get; set; } = new();
}

public class RedditListingData
{
    [JsonPropertyName("modhash")]
    public string Modhash { get; set; } = "";

    [JsonPropertyName("dist")]
    public int Dist { get; set; }

    [JsonPropertyName("children")]
    public List<RedditPostWrapper> Children { get; set; } = new();
}

public class RedditPostWrapper
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "";

    [JsonPropertyName("data")]
    public RedditPostData Data { get; set; } = new();
}

public class RedditPostData
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("permalink")]
    public string Permalink { get; set; } = "";

    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("is_gallery")]
    public bool IsGallery { get; set; }

    [JsonPropertyName("over_18")]
    public bool IsNsfw { get; set; }

    [JsonPropertyName("media_metadata")]
    public Dictionary<string, RedditMediaMetadata>? MediaMetadata { get; set; }

    [JsonPropertyName("post_hint")]
    public string? PostHint { get; set; }
}

public class RedditMediaMetadata
{
    [JsonPropertyName("m")]
    public string MimeType { get; set; } = "";

    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
}
