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

/* 
namespace SlashCommands;

[SlashCommand("feed", "Manage feeds for this server")]
public class Feeds :  ApplicationCommandModule<ApplicationCommandContext>
{
    const int TOPIC_LENGTH_MAX = 32;
    const int TOPIC_LENGTH_MIN = 2;
    const int INTERVAL_IN_MINUTES_MAX = 1440;
    const int INTERVAL_IN_MINUTES_MIN = 5;
    const int START_AT_MINUTE_MIN = 1;
    const int START_AT_MINUTE_MAX = 60;

    private readonly FeedService _feedService = MainServiceProvider.GetRequiredService<FeedService>();


    [DefferedResponse]
    [SubSlashCommand("list", "Lists all active feeds on this server")]
    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    public async Task ListFeeds()
    {
        try
        {
            // Retrieve and sort feeds for current guild
            List<FeedProperties> feeds = await _feedService.GetActiveFeedsAsync(Context.Interaction.Guild!.Id);
            feeds = feeds.OrderByDescending(x => x.CreatedAt).ToList();

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
                    var field = new EmbedFieldProperties
                    {
                        Name = feed.Topic,
                        Value = ResourceHelper.GetString(
                            Strings.Commands.FeedDelay,
                            Context.Interaction.UserLocale,
                            feed.IntervalInMinutes
                        )
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
        catch (Exception ex)
        {
            throw new SlashCommandGenericException(ex.Message);
        }

    }

    [DefferedResponse]
    [SubSlashCommand("delete", "Deletes a feed from this server")]
    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    public async Task DeleteFeed(
        [SlashCommandParameter(
            Name = "feedid",
            Description = "ID of the feed to delete"
        )]
        int feedId
    )
    {
        try
        {
            await _feedService.RemoveFeedAsync(Context.Interaction.Guild!.Id, feedId);

            await Context.Interaction.SendFollowupMessageAsync(
                "lorem ipsum"
            );
        }
        catch (Exception ex)
        {
            throw new SlashCommandGenericException(ex.Message);
        }
    }

    [DefferedResponse]
    [SubSlashCommand("create", "Creates a new feed, to regularly send content from a Subreddit, in this channel")]
    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    public async Task CreateNewFeed(
        [SlashCommandParameter(
            Name = "topic",
            Description = "Subreddit's name",
            MaxLength = TOPIC_LENGTH_MAX,
            MinLength = TOPIC_LENGTH_MIN
        )]
        string topic,
        [SlashCommandParameter(
            Name = "interval",
            Description = "Interval in minute",
            MaxValue = INTERVAL_IN_MINUTES_MAX,
            MinValue = INTERVAL_IN_MINUTES_MIN
        )]
        int intervalInMinutes,
        [SlashCommandParameter(
            Name = "start_at",
            Description = "Decide at which minute of the current hour to start posting",
            MaxValue = START_AT_MINUTE_MAX,
            MinValue = START_AT_MINUTE_MIN
        )]
        int startAtMinute = -1
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
                    startAtMinute
                )
            );

            await Context.Interaction.SendFollowupMessageAsync(
                ResourceHelper.GetString(Strings.Commands.FeedCreated, Context.Interaction.UserLocale, topic)
            );
        }
        // classic exception
        catch (Exception ex)
        {
            throw new SlashCommandGenericException(ex.Message);
        }
    }
}
*/