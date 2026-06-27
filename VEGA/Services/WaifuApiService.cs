using System.Text.Json;
using Core;
using Exceptions;
using Microsoft.AspNetCore.WebUtilities;
using Models.Business;
using Resources;

namespace Services;

public class WaifuApiService(IHttpClientFactory httpClientFactory)
{
    private readonly HttpClient _http = httpClientFactory.CreateClient(HttpClientNames.AnimeImages);

    public async Task<List<string>> FetchImagesAsync(int count, int categoryId)
    {
        IApiDefinition apiToFetch;
        List<IApiDefinition> matchingApis = WaifuApiTypes.GetApisContainingCategory(categoryId).ToList();

        // Decide on which API to call (decided by which one contained the correct category)
        if (matchingApis.Count > 1)
        {
            // Choose at random between APIs containing category
            int apiIndex = Random.Shared.Next(0, matchingApis.Count);
            apiToFetch = matchingApis[apiIndex];
        }
        else 
            apiToFetch = matchingApis.First() ?? throw new SlashCommandBusinessException(Strings.Exceptions.InvalidCategorySelected);

        return apiToFetch switch
        {
            WaifuPicsApiReference => await FetchApiWaifuPicAsync(count, categoryId),
            WaifuImApiReference   => await FetchApiWaifuImAsync(count, categoryId),
            NekosiaApiReference   => await FetchApiNekosiaAsync(count, categoryId),
            _ => throw new SlashCommandBusinessException(Strings.Exceptions.UnimplementedApi),
        };
    }

    private async Task<List<string>> FetchApiWaifuPicAsync(int count, int categoryId)
    {
        IApiDefinition apiDefinition = WaifuApiTypes.WaifuPics;
        bool multiple = count > 1;
        string categoryValue = apiDefinition.Categories.Single(x => x.Id == categoryId).Value;

        string baseUri = apiDefinition.GetBaseUri(multiple);
        string fullUrl = string.Format(baseUri, "sfw", categoryValue);

        List<string> imgUrls = new();

        if (multiple)
        {
            HttpResponseMessage response = await _http.PostAsync(
                fullUrl,
                // The "exclude" field is required, even when empty
                new FormUrlEncodedContent(new Dictionary<string, string> {{"exclude", ""}})
            );
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            var res = JsonSerializer.Deserialize<WaifuPicsApiReference.MultiplePicsApiResponse>(json);
            imgUrls.AddRange(res!.PicUrls.Take(count));
        }
        else
        {
            HttpResponseMessage response = await _http.GetAsync(fullUrl);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            var item = JsonSerializer.Deserialize<WaifuPicsApiReference.SinglePicResponse>(json);
            imgUrls.Add(item!.Url!);
        }
        
        return imgUrls;
    }

    private async Task<List<string>> FetchApiWaifuImAsync(int count, int categoryId)
    {
        IApiDefinition apiDefinition = WaifuApiTypes.WaifuIm;
        string categoryValue = apiDefinition.Categories.Single(x => x.Id == categoryId).Value;
        string baseUri = apiDefinition.GetBaseUri();
        
        var queryParams = new Dictionary<string, string?>
        {
            ["IsNsfw"] = false.ToString(),
            ["IncludedTags"] = categoryValue,
            ["PageSize"] = count.ToString()
        };
        if (count > 1)
            queryParams.Add("limit", count.ToString());
        
        string fullUrl = QueryHelpers.AddQueryString(baseUri, queryParams);

        HttpResponseMessage response = await _http.GetAsync(fullUrl);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync();

        var res = JsonSerializer.Deserialize<WaifuImApiReference.MultiplePicApiResponse>(json);
        
        return res!.Images.Select(x => x.Url).ToList();
    }

    private async Task<List<string>> FetchApiNekosiaAsync(int count, int categoryId)
    {
        IApiDefinition apiDefinition = WaifuApiTypes.Nekosia;
        string categoryValue = apiDefinition.Categories.Single(x => x.Id == categoryId).Value;
        string baseUri = apiDefinition.GetBaseUri();
        string url = string.Format(baseUri, categoryValue);
        bool multiple = count > 1;
        
        var queryParams = new Dictionary<string, string?>
        {
            ["rating"] = "safe"
        };
        if (multiple)
            queryParams.Add("count", count.ToString());
        
        string fullUrl = QueryHelpers.AddQueryString(url, queryParams);

        HttpResponseMessage response = await _http.GetAsync(fullUrl);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync();

        List<string> imgUrls = new();
        
        if (multiple)
        {
            var res = JsonSerializer.Deserialize<NekosiaApiReference.MultiplePicsApiResponse>(json);
            imgUrls.AddRange(res!.Images.Select(x => x.Image.original.url));
        }
        else
        {
            var res = JsonSerializer.Deserialize<NekosiaApiReference.SinglePicResponse>(json);
            imgUrls.Add(res!.Image.original.url);
        }
        
        return imgUrls;
    }
}