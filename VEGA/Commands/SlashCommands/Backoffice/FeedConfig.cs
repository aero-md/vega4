using static Core.GlobalRegistry;
using Exceptions;
using NetCord;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using Microsoft.Extensions.DependencyInjection;
using Services;
using Core.CustomCommandAttributes;
using Resources;
using Models.Entities;

namespace SlashCommands;

[BackofficeCommand]
[SlashCommand("feedconfig", "Manage feed system configuration (SuperAdmin only)")]
public class FeedConfig : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly FeedContentService _feedContentService = MainServiceProvider.GetRequiredService<FeedContentService>();

    [RequireSuperAdmin]
    [DefferedResponse]
    [SubSlashCommand("show", "Display current feed configuration")]
    public async Task ShowConfig()
    {
        try
        {
            var config = await _feedContentService.GetConfigAsync();

            var embed = new EmbedProperties
            {
                Title = "Feed Configuration",
                Fields = new List<EmbedFieldProperties>
                {
                    new() { Name = "fetch_size", Value = config.FetchSize.ToString(), Inline = true },
                    new() { Name = "fetch_interval_minutes", Value = config.FetchIntervalMinutes.ToString(), Inline = true },
                    new() { Name = "history_size", Value = config.HistorySize.ToString(), Inline = true },
                    new() { Name = "sort_mode", Value = config.SortMode, Inline = true },
                    new() { Name = "max_feeds_per_guild", Value = config.MaxFeedsPerGuild.ToString(), Inline = true },
                }
            };

            await Context.Interaction.SendFollowupMessageAsync(
                new InteractionMessageProperties { Embeds = new[] { embed } }
            );
        }
        catch (Exception ex)
        {
            throw new SlashCommandGenericException(ex.Message);
        }
    }

    [RequireSuperAdmin]
    [DefferedResponse]
    [SubSlashCommand("set", "Update a feed configuration parameter")]
    public async Task SetConfig(
        [SlashCommandParameter(
            Name = "parameter",
            Description = "The configuration parameter to update"
        )]
        FeedConfigParameter parameter,
        [SlashCommandParameter(
            Name = "value",
            Description = "The new value for the parameter"
        )]
        string value
    )
    {
        try
        {
            var config = await _feedContentService.GetConfigAsync();

            switch (parameter)
            {
                case FeedConfigParameter.FetchSize:
                    if (!int.TryParse(value, out var fetchSize) || fetchSize < 1 || fetchSize > 100)
                        throw new SlashCommandBusinessException(
                            ResourceHelper.GetString(Strings.Exceptions.FeedConfigInvalidValue, Context.Interaction.UserLocale, "fetch_size", "1-100"));
                    config.FetchSize = fetchSize;
                    break;

                case FeedConfigParameter.FetchIntervalMinutes:
                    if (!int.TryParse(value, out var interval) || interval < 1)
                        throw new SlashCommandBusinessException(
                            ResourceHelper.GetString(Strings.Exceptions.FeedConfigInvalidValue, Context.Interaction.UserLocale, "fetch_interval_minutes", "≥ 1"));
                    config.FetchIntervalMinutes = interval;
                    break;

                case FeedConfigParameter.HistorySize:
                    if (!int.TryParse(value, out var historySize) || historySize < 1)
                        throw new SlashCommandBusinessException(
                            ResourceHelper.GetString(Strings.Exceptions.FeedConfigInvalidValue, Context.Interaction.UserLocale, "history_size", "≥ 1"));
                    config.HistorySize = historySize;
                    break;

                case FeedConfigParameter.SortMode:
                    var validModes = FeedConfiguration.GetValidSortModes();
                    if (!validModes.Contains(value.ToLowerInvariant()))
                        throw new SlashCommandBusinessException(
                            ResourceHelper.GetString(Strings.Exceptions.FeedConfigInvalidValue, Context.Interaction.UserLocale, "sort_mode", string.Join(", ", validModes)));
                    config.SortMode = value.ToLowerInvariant();
                    break;

                case FeedConfigParameter.MaxFeedsPerGuild:
                    if (!int.TryParse(value, out var maxFeeds) || maxFeeds < 1)
                        throw new SlashCommandBusinessException(
                            ResourceHelper.GetString(Strings.Exceptions.FeedConfigInvalidValue, Context.Interaction.UserLocale, "max_feeds_per_guild", "≥ 1"));
                    config.MaxFeedsPerGuild = maxFeeds;
                    break;
            }

            await _feedContentService.UpdateConfigAsync(config);

            await Context.Interaction.SendFollowupMessageAsync(
                ResourceHelper.GetString(Strings.Commands.FeedConfigUpdated, Context.Interaction.UserLocale, parameter.ToString(), value)
            );
        }
        catch (SlashCommandBusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SlashCommandGenericException(ex.Message);
        }
    }
}

/// <summary>
/// Enum for feed configuration parameters — displayed as Discord slash command choices
/// </summary>
public enum FeedConfigParameter
{
    [SlashCommandChoice(Name = "fetch_size")]
    FetchSize,

    [SlashCommandChoice(Name = "fetch_interval_minutes")]
    FetchIntervalMinutes,

    [SlashCommandChoice(Name = "history_size")]
    HistorySize,

    [SlashCommandChoice(Name = "sort_mode")]
    SortMode,

    [SlashCommandChoice(Name = "max_feeds_per_guild")]
    MaxFeedsPerGuild,
}
