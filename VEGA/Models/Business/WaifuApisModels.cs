using System;
using System.Text.Json.Serialization;
using Microsoft.VisualBasic;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace Models.Business;

public class WaifuCategoryValue
{
    public int Id {get; set;}
    public string Value {get; set;}


    /// <summary>
    /// Use WaifuCategoryChoicesProvider for reference on categories IDs.
    /// Value is the string value expected as argument by the API
    /// </summary>
    /// <param name="id"></param>
    /// <param name="value"></param>
    public WaifuCategoryValue(int id, string value)
    {
        Id = id;
        Value = value;
    }
}

public static class WaifuApiTypes
{
    public static List<IApiDefinition> ApiDefinitions => [WaifuIm, WaifuPics, Nekosia];

    public static readonly IApiDefinition WaifuPics = new WaifuPicsApiReference();
    public static readonly IApiDefinition WaifuIm = new WaifuImApiReference();
    public static readonly IApiDefinition Nekosia = new NekosiaApiReference();

    /// <summary>
    /// Returns API definitions containing the required category
    /// </summary>
    /// <param name="categoryId"></param>
    /// <returns></returns>
    public static IEnumerable<IApiDefinition> GetApisContainingCategory(int categoryId)
    {
        return ApiDefinitions.Where(x => x.Categories.Exists(y => y.Id == categoryId));
    }
}

public interface IApiDefinition
{
    public class SinglePicResponse{};
    public class MultiplePicsApiResponse{};

    public string GetBaseUri(bool multiple = false);
    
    /// <summary>
    /// All categories. NSFW Categories begin at ID 1000
    /// </summary>
    public List<WaifuCategoryValue> Categories { get; }
}

/// <summary>
/// Models and consts for nekosia.cat
/// Reference : https://nekosia.cat/documentation?page=endpoints
/// </summary>
public class NekosiaApiReference : IApiDefinition
{
    public class SinglePicResponse{
        [JsonPropertyName("image")]
        public required ResponseImageItem Image {get; set;}
    }
    public class MultiplePicsApiResponse{
        [JsonPropertyName("images")]
        public required IEnumerable<SinglePicResponse> Images {get; set;}
    }

    public record ResponseImageItem(OriginalImgLinks original);
    public record OriginalImgLinks(string url, string extension);


    public string GetBaseUri(bool multiple = false){
        return "https://api.nekosia.cat/api/v1/images/{0}";
    }

    public List<WaifuCategoryValue> Categories => [
        new(0, "random"),
        new(1, "catgirl"),
        new(2, "maid"),
        new(3, "uniform"),
        new(4, "foxgirl"),
        new(7, "vtuber"),
    ];
}

/// <summary>
/// Models and consts for waifu.pics.
/// Reference : https://waifu.pics/docs
/// </summary>
public class WaifuPicsApiReference : IApiDefinition
{
    public class SinglePicResponse{
        [JsonPropertyName("url")]
        public required string Url {get; set;}
    }

    public class MultiplePicsApiResponse{
        [JsonPropertyName("files")]
        public required IEnumerable<string> PicUrls {get; set;}
    }

    public string GetBaseUri(bool multiple = false){
        return multiple ? "https://api.waifu.pics/many/{0}/{1}" : "https://api.waifu.pics/{0}/{1}";
    }

    public List<WaifuCategoryValue> Categories => [
        new(0, "waifu"),
        new(102, "megumin"),
        new(1, "neko"),
        new(101, "shinobu")
    ];
}


/// <summary>
/// Models and consts for waifu.im
/// Reference : https://docs.waifu.im
/// </summary>
public class WaifuImApiReference : IApiDefinition
{
    public class MultiplePicApiResponse
    {
        [JsonPropertyName("images")]
        public required IEnumerable<ResponseImageItem> Images {get; set;}
    }
    public class ResponseImageItem
    {
        [JsonPropertyName("url")]
        public required string Url {get; set;}
    }

    public string GetBaseUri(bool multiple = false){
        return "https://api.waifu.im/search";
    }

    public List<WaifuCategoryValue> Categories =>
    [
        new(0, "waifu"),
        new(2, "maid"),
        new(3, "uniform"),
        new(5, "oppai"),
        new(103, "marin-kitagawa"),
        new(105, "raiden-shogun"),
    ];
}

public class SfwWaifuCategoryChoicesProvider : IChoicesProvider<ApplicationCommandContext>
{
    public ValueTask<IEnumerable<ApplicationCommandOptionChoiceProperties>?> GetChoicesAsync(SlashCommandParameter<ApplicationCommandContext> parameter)
    {
        var list = new List<ApplicationCommandOptionChoiceProperties>
        {
            new("Any", 0),
            new("Cat girl", 1),
            new("Maid", 2),
            new("Uniform", 3),
            new("Fox girl", 4),
            new("Boobs", 5),
            new("Vtuber", 7),
            new("Shinobu Oshino", 101),
            new("Megumin", 102),
            new("Marin Kitagawa", 103),
            new("Raiden Shogun", 105),
        };

        return ValueTask.FromResult(list.AsEnumerable())!;
    }
}