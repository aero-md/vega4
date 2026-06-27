using System.Text.Json;
using System.Text.Json.Serialization;
using NetCord;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using Models.Business;
using static Core.GlobalRegistry;
using Services;
using Microsoft.Extensions.DependencyInjection;
using Exceptions;
using Resources;
using Core.CustomCommandAttributes;

namespace SlashCommands;

public class GetWaifu : ApplicationCommandModule<ApplicationCommandContext>
{
    public const int COUNT_MIN = 1;
    public const int COUNT_MAX = 5;

    [DefferedResponse]
    [RequireUserPermissions<ApplicationCommandContext>(Permissions.AttachFiles)]
    [RequireBotPermissions<ApplicationCommandContext>(Permissions.AttachFiles)]
    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [SlashCommand("waifu", "Sends waifu images")]
    public async Task Execute(
        [SlashCommandParameter(
            Name = "type", 
            Description = "Type of waifu to send",
            ChoicesProviderType = typeof(SfwWaifuCategoryChoicesProvider)
        )] int type = 0,
        [SlashCommandParameter(
            Name = "count", Description = "Number of waifu to send", MinValue = 1, MaxValue = 5
        )] int count = 1
    )
    {
        // Don't trust Discord on minmax values validation
        if (
            count > COUNT_MAX || count < COUNT_MIN
        ) throw new SlashCommandBusinessException(Strings.Exceptions.InvalidParams);

        var waifuApiService = MainServiceProvider.GetRequiredService<WaifuApiService>();
        
        try
        {
            List<string> imageUrls = await waifuApiService.FetchImagesAsync(count, type);
            
            string response = string.Join("\n",imageUrls);

            await Context.Interaction.SendFollowupMessageAsync(response);
        }
        // API error : business exception with explicit message
        catch (HttpRequestException httpEx)
        {
            throw new SlashCommandBusinessException(Strings.Exceptions.WaifuApiCallFailed, httpEx.StatusCode?.ToString() ?? Strings.Misc.Unknown);
        }
        // Other : classic exception
        catch (Exception ex)
        {
            throw new SlashCommandGenericException(ex.Message);
        }
    }
}