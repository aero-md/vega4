using static Core.GlobalRegistry;
using Exceptions;
using Models.Entities;
using NetCord;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using Microsoft.Extensions.DependencyInjection;
using Services;
using Core.CustomCommandAttributes;
using Resources;

namespace SlashCommands;

[SlashCommand("feed", "Manage feeds for this server")]
public class Feeds :  ApplicationCommandModule<ApplicationCommandContext>
{
    const int TOPIC_LENGTH_MAX = 21;
    const int TOPIC_LENGTH_MIN = 2;
    const int INTERVAL_IN_MINUTES_MAX = 1440;
    const int INTERVAL_IN_MINUTES_MIN = 15;
    const int START_AT_MINUTE_MIN = 0;
    const int START_AT_MINUTE_MAX = 59;

    private readonly FeedService _feedService = MainServiceProvider.GetRequiredService<FeedService>();


    [DefferedResponse]
    [SubSlashCommand("list", "Lists all feeds on this server")]
    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [RequireUserPermissions<ApplicationCommandContext>(Permissions.ManageMessages)]
    public async Task ListFeeds()
    {
        try
        {
            // Retrieve all feeds for current guild (including inactive)
            List<FeedProperties> feeds = await _feedService.GetFeedsAsync(Context.Interaction.Guild!.Id);

            if (feeds.Count == 0)
            {
                throw new SlashCommandBusinessException(Strings.Commands.NoActiveFeedsOnServer);
            }
            else
            {
                var fields = new List<EmbedFieldProperties>();

                var embed = new EmbedProperties
                {
                    Title = ResourceHelper.GetString(Strings.Commands.ActiveFeedsOnServer, Context.Interaction.UserLocale),
                };

                foreach (var feed in feeds)
                {
                    var nsfwIndicator = feed.AllowNsfw ? " 🔞" : "";
                    var statusText = GetFeedStatusText(feed.Status, Context.Interaction.UserLocale);
                    var field = new EmbedFieldProperties
                    {
                        Name = $"r/{feed.Topic}{nsfwIndicator}",
                        Value = $"ID: `{feed.FeedId}`\n" 
                            + ResourceHelper.GetString(
                                Strings.Commands.FeedDelay,
                                Context.Interaction.UserLocale,
                                feed.IntervalInMinutes
                            ) 
                            + $"\n{statusText}"
                    };
                    fields.Add(field);
                }

                if (fields.Count > 0)
                    embed.Fields = fields;

                await Context.Interaction.SendFollowupMessageAsync(
                    new InteractionMessageProperties
                    {
                        Embeds = new[] { embed }
                    }
                );
            }
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

    private static string GetFeedStatusText(FeedStatus status, string? locale)
    {
        var loc = locale ?? "en-US";
        return status switch
        {
            FeedStatus.Active => ResourceHelper.GetString(Strings.Commands.FeedStatusActive, loc),
            FeedStatus.ChannelDeleted => ResourceHelper.GetString(Strings.Commands.FeedStatusChannelDeleted, loc),
            FeedStatus.TopicUnavailable => ResourceHelper.GetString(Strings.Commands.FeedStatusTopicUnavailable, loc),
            FeedStatus.Suspended => ResourceHelper.GetString(Strings.Commands.FeedStatusSuspended, loc),
            _ => status.ToString()
        };
    }

    [DefferedResponse]
    [SubSlashCommand("delete", "Deletes a feed from this server")]
    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [RequireUserPermissions<ApplicationCommandContext>(Permissions.ManageMessages)]
    public async Task DeleteFeed(
        [SlashCommandParameter(
            Name = "feedid",
            Description = "UUID of the feed to delete (use /feed list to see IDs)"
        )]
        string feedIdString
    )
    {
        try
        {
            if (!Guid.TryParse(feedIdString, out var feedId))
            {
                throw new SlashCommandBusinessException(Strings.Exceptions.InvalidFeedIdFormat);
            }

            await _feedService.RemoveFeedAsync(Context.Interaction.Guild!.Id, feedId);

            await Context.Interaction.SendFollowupMessageAsync(
                ResourceHelper.GetString(Strings.Commands.FeedDeleted, Context.Interaction.UserLocale)
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

    [DefferedResponse]
    [SubSlashCommand("create", "Creates a new feed, to regularly send content from a Subreddit, in this channel")]
    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [RequireUserPermissions<ApplicationCommandContext>(Permissions.ManageMessages)]
    public async Task CreateNewFeed(
        [SlashCommandParameter(
            Name = "topic",
            Description = "Subreddit's name (without r/)",
            MaxLength = TOPIC_LENGTH_MAX,
            MinLength = TOPIC_LENGTH_MIN
        )]
        string topic,
        [SlashCommandParameter(
            Name = "interval",
            Description = "Interval in minutes between posts",
            MaxValue = INTERVAL_IN_MINUTES_MAX,
            MinValue = INTERVAL_IN_MINUTES_MIN
        )]
        int intervalInMinutes,
        [SlashCommandParameter(
            Name = "start_at",
            Description = "Minute of the hour to start posting (0-59)",
            MaxValue = START_AT_MINUTE_MAX,
            MinValue = START_AT_MINUTE_MIN
        )]
        int startAtMinute = -1,
        [SlashCommandParameter(
            Name = "allow_nsfw",
            Description = "Allow NSFW content"
        )]
        bool allowNsfw = false
    )
    {
        try
        {
            await _feedService.CreateNewFeedAsync(
                new FeedProperties(
                    Context.Interaction.Guild!.Id,
                    Context.Interaction.Channel.Id,
                    topic,
                    intervalInMinutes,
                    startAtMinute,
                    allowNsfw
                )
            );

            await Context.Interaction.SendFollowupMessageAsync(
                ResourceHelper.GetString(Strings.Commands.FeedCreated, Context.Interaction.UserLocale, topic)
            );
        }
        catch (SlashCommandBusinessException)
        {
            throw;
        }
        catch (ArgumentException ex)
        {
            // Validation errors from FeedContentService.ValidateSubreddit
            throw new SlashCommandBusinessException(ex.Message);
        }
        catch (Exception ex)
        {
            throw new SlashCommandGenericException(ex.Message);
        }
    }
}