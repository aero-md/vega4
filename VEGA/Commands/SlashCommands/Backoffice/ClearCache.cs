using static Core.GlobalRegistry;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using Microsoft.Extensions.DependencyInjection;
using Services;
using Core.CustomCommandAttributes;
using Resources;
using NetCord.Rest;
using NetCord;
using Exceptions;

namespace SlashCommands;

[BackofficeCommand]
[SlashCommand("clearcache", "Clear cached data for a guild (Backoffice command)")]
public class ClearCache : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly GuildSettingsService _guildSettingsService = MainServiceProvider.GetRequiredService<GuildSettingsService>();

    [SubSlashCommand("guild", "Clear cache for a specific guild")]
    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    public async Task ClearGuildCache(
        [SlashCommandParameter(
            Name = "guild_id",
            Description = "The ID of the guild to clear cache for"
        )]
        string guildIdStr
    )
    {
        if (!ulong.TryParse(guildIdStr, out ulong guildId))
            throw new SlashCommandBusinessException(Strings.Commands.InvalidGuildIdFormat);

        bool cacheExisted = _guildSettingsService.ClearCacheForGuild(guildId);

        string messageKey = cacheExisted 
            ? Strings.Commands.CacheClearedForGuild 
            : Strings.Commands.CacheNotFoundForGuild;

        await Context.Interaction.SendResponseAsync(
            InteractionCallback.Message(
                new InteractionMessageProperties
                {
                    Content = ResourceHelper.GetString(messageKey, Context.Interaction.UserLocale),
                    Flags = MessageFlags.Ephemeral
                }
            )
        );
    }

    [SubSlashCommand("current", "Clear cache for the current guild")]
    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    public async Task ClearCurrentGuildCache()
    {
        var guildId = Context.Interaction.Guild!.Id;
        
        bool cacheExisted = _guildSettingsService.ClearCacheForGuild(guildId);

        string messageKey = cacheExisted 
            ? Strings.Commands.CacheClearedForCurrentGuild 
            : Strings.Commands.CacheNotFoundForCurrentGuild;

        await Context.Interaction.SendResponseAsync(
            InteractionCallback.Message(
                new InteractionMessageProperties
                {
                    Content = ResourceHelper.GetString(messageKey, Context.Interaction.UserLocale),
                    Flags = MessageFlags.Ephemeral
                }
            )
        );
    }

    [SubSlashCommand("info", "Display information about the cache system")]
    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    public async Task ShowCacheInfo()
    {
        var embed = new EmbedProperties
        {
            Title = ResourceHelper.GetString(Strings.Commands.CacheInfoTitle, Context.Interaction.UserLocale),
            Description = ResourceHelper.GetString(Strings.Commands.CacheInfoDescription, Context.Interaction.UserLocale),
            Color = new Color(0x3498db),
            Fields = new[]
            {
                new EmbedFieldProperties
                {
                    Name = ResourceHelper.GetString(Strings.Commands.CacheInfoDurationLabel, Context.Interaction.UserLocale),
                    Value = ResourceHelper.GetString(Strings.Commands.CacheInfoDurationValue, Context.Interaction.UserLocale),
                    Inline = true
                },
                new EmbedFieldProperties
                {
                    Name = ResourceHelper.GetString(Strings.Commands.CacheInfoDataLabel, Context.Interaction.UserLocale),
                    Value = ResourceHelper.GetString(Strings.Commands.CacheInfoDataValue, Context.Interaction.UserLocale),
                    Inline = true
                }
            },
            Footer = new EmbedFooterProperties
            {
                Text = ResourceHelper.GetString(Strings.Commands.CacheInfoFooter, Context.Interaction.UserLocale)
            }
        };

        await Context.Interaction.SendResponseAsync(
            InteractionCallback.Message(
                new InteractionMessageProperties
                {
                    Embeds = new[] { embed }
                }
            )
        );
    }
}
