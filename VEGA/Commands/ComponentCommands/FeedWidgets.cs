using Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Models.Entities;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;
using Resources;
using Serilog;
using Services;
using static Core.GlobalRegistry;

namespace ComponentCommands;

/// <summary>
/// Shared renderer for the /feed list message (embed + Add/Delete buttons), so the slash
/// command and the widgets produce the same view and a refresh is a single edit.
/// </summary>
public static class FeedListView
{
    public static EmbedProperties BuildEmbed(IReadOnlyList<FeedProperties> feeds, string? locale)
    {
        locale ??= "en-US";
        var embed = new EmbedProperties
        {
            Title = ResourceHelper.GetString(Strings.Commands.ActiveFeedsOnServer, locale)
        };

        if (feeds.Count == 0)
        {
            embed.Description = ResourceHelper.GetString(Strings.Commands.NoActiveFeedsOnServer, locale);
            return embed;
        }

        var fields = new List<EmbedFieldProperties>();
        for (int i = 0; i < feeds.Count; i++)
        {
            var f = feeds[i];
            var nsfw = f.AllowNsfw ? " 🔞" : "";
            var delay = ResourceHelper.GetString(Strings.Commands.FeedDelay, locale, f.IntervalInMinutes);
            fields.Add(new EmbedFieldProperties
            {
                Name = $"{i + 1}.",
                Value = $"`r/{f.Topic}`{nsfw}\n-# {delay} · {StatusText(f.Status, locale)}"
            });
        }

        embed.Fields = fields;
        return embed;
    }

    public static IEnumerable<IMessageComponentProperties> BuildComponents(IReadOnlyList<FeedProperties> feeds, string? locale)
    {
        locale ??= "en-US";
        var buttons = new List<IActionRowComponentProperties>
        {
            new ButtonProperties(
                FeedWidgetButtons.CUSTOMID_ADD_OPEN,
                ResourceHelper.GetString(Strings.Commands.FeedBtnAdd, locale),
                ButtonStyle.Success)
        };

        if (feeds.Count > 0)
        {
            buttons.Add(new ButtonProperties(
                FeedWidgetButtons.CUSTOMID_DELETE_OPEN,
                ResourceHelper.GetString(Strings.Commands.FeedBtnDelete, locale),
                ButtonStyle.Danger));
        }

        return new IMessageComponentProperties[] { new ActionRowProperties(buttons) };
    }

    public static string StatusText(FeedStatus status, string? locale)
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
}

/// <summary>
/// Button half of the /feed management widget.
///   /feed list → [➕ Add] (modal) and [🗑️ Delete] (select menu → confirm).
/// The originating list message id is threaded through the custom_ids so the public list
/// message can be refreshed after the action. Prefixes must not contain CUSTOMID_SEP (':').
/// </summary>
public class FeedWidgetButtons : ComponentInteractionModule<ButtonInteractionContext>
{
    public const string CUSTOMID_ADD_OPEN = "feed_addopen";
    public const string CUSTOMID_ADD_MODAL = "feed_addmodal";
    public const string CUSTOMID_DELETE_OPEN = "feed_delopen";
    public const string CUSTOMID_DELETE_SELECT = "feed_delselect";
    public const string CUSTOMID_DELETE_CONFIRM = "feed_delconfirm";
    public const string CUSTOMID_DELETE_CANCEL = "feed_delcancel";
    public const char CUSTOMID_SEP = ':';
    public const char CUSTOMID_INNER_SEP = '_';
    // Sentinel used when the widget is opened directly from a slash command (no list message to refresh).
    public const string NO_LIST = "0";

    // Modal text-input ids (internal to the modal, not routed by [ComponentInteraction]).
    public const string ADD_FIELD_TOPIC = "feed_add_topic";
    public const string ADD_FIELD_INTERVAL = "feed_add_interval";
    public const string ADD_FIELD_STARTAT = "feed_add_startat";
    public const string ADD_FIELD_NSFW = "feed_add_nsfw";

    public const int TOPIC_MIN = 2;
    public const int TOPIC_MAX = 21;
    public const int INTERVAL_MIN = 15;
    public const int INTERVAL_MAX = 1440;
    public const int START_AT_MIN = 0;
    public const int START_AT_MAX = 59;

    private const int MAX_MENU_OPTIONS = 25;
    private const int OPTION_TEXT_MAX = 100;

    /// <summary>"➕ Add" button → opens the creation modal (carrying the list message id).</summary>
    [ComponentInteraction(CUSTOMID_ADD_OPEN)]
    public async Task OpenAddModalAsync()
    {
        var locale = Context.Interaction.UserLocale ?? "en-US";

        if (Context.Interaction.GuildId is null || !CanManage())
        {
            await RespondEphemeralAsync(ResourceHelper.GetString(Strings.Exceptions.FeedManageRequired, locale));
            return;
        }

        await Context.Interaction.SendResponseAsync(
            InteractionCallback.Modal(BuildAddModal(Context.Message.Id.ToString(), locale)));
    }

    /// <summary>"🗑️ Delete" button → opens the select menu (carrying the list message id).</summary>
    [ComponentInteraction(CUSTOMID_DELETE_OPEN)]
    public async Task OpenDeleteMenuAsync()
    {
        var locale = Context.Interaction.UserLocale ?? "en-US";

        if (Context.Interaction.GuildId is not ulong guildId || !CanManage())
        {
            await RespondEphemeralAsync(ResourceHelper.GetString(Strings.Exceptions.FeedManageRequired, locale));
            return;
        }

        var feedService = MainServiceProvider.GetRequiredService<FeedService>();
        var feeds = await feedService.GetFeedsAsync(guildId);

        await Context.Interaction.SendResponseAsync(
            InteractionCallback.Message(BuildDeleteMenuMessage(feeds, Context.Message.Id.ToString(), locale)));
    }

    /// <summary>
    /// Builds the feed-creation modal. <paramref name="listMsgId"/> threads the /feed list message
    /// to refresh on submit (use <see cref="NO_LIST"/> when opened directly via /feed add).
    /// </summary>
    public static ModalProperties BuildAddModal(string listMsgId, string locale) =>
        new ModalProperties(
            $"{CUSTOMID_ADD_MODAL}{CUSTOMID_SEP}{listMsgId}",
            ResourceHelper.GetString(Strings.Commands.FeedAddModalTitle, locale),
            new IModalComponentProperties[]
            {
                new LabelProperties(
                    ResourceHelper.GetString(Strings.Commands.FeedAddFieldTopic, locale),
                    new TextInputProperties(ADD_FIELD_TOPIC, TextInputStyle.Short)
                    { Required = true, MinLength = TOPIC_MIN, MaxLength = TOPIC_MAX }),
                new LabelProperties(
                    ResourceHelper.GetString(Strings.Commands.FeedAddFieldInterval, locale),
                    new TextInputProperties(ADD_FIELD_INTERVAL, TextInputStyle.Short)
                    { Required = true, MaxLength = 4 }),
                new LabelProperties(
                    ResourceHelper.GetString(Strings.Commands.FeedAddFieldStartAt, locale),
                    new TextInputProperties(ADD_FIELD_STARTAT, TextInputStyle.Short)
                    { Required = false, MaxLength = 2 }),
                new LabelProperties(
                    ResourceHelper.GetString(Strings.Commands.FeedAddFieldNsfw, locale),
                    new TextInputProperties(ADD_FIELD_NSFW, TextInputStyle.Short)
                    { Required = false, MaxLength = 3 })
            });

    /// <summary>
    /// Builds the ephemeral delete picker (or a "no feeds" message). <paramref name="listMsgId"/>
    /// is threaded for refresh (use <see cref="NO_LIST"/> when opened directly via /feed delete).
    /// </summary>
    public static InteractionMessageProperties BuildDeleteMenuMessage(IEnumerable<FeedProperties> allFeeds, string listMsgId, string locale)
    {
        var feeds = allFeeds.OrderBy(f => f.CreatedAt).Take(MAX_MENU_OPTIONS).ToList();

        if (feeds.Count == 0)
            return new InteractionMessageProperties
            {
                Content = ResourceHelper.GetString(Strings.Commands.NoActiveFeedsOnServer, locale),
                Flags = MessageFlags.Ephemeral
            };

        var options = feeds.Select(f =>
        {
            var opt = new StringMenuSelectOptionProperties(Truncate($"r/{f.Topic}", OPTION_TEXT_MAX), f.FeedId.ToString("N"));
            opt.Description = Truncate($"{ResourceHelper.GetString(Strings.Commands.FeedDelay, locale, f.IntervalInMinutes)} · {FeedListView.StatusText(f.Status, locale)}", OPTION_TEXT_MAX);
            return opt;
        });

        var menu = new StringMenuProperties($"{CUSTOMID_DELETE_SELECT}{CUSTOMID_SEP}{listMsgId}", options)
        {
            Placeholder = ResourceHelper.GetString(Strings.Commands.FeedDeleteMenuPlaceholder, locale),
            MinValues = 1,
            MaxValues = 1
        };

        return new InteractionMessageProperties
        {
            Content = ResourceHelper.GetString(Strings.Commands.FeedDeleteMenuPrompt, locale),
            Components = new IMessageComponentProperties[] { menu },
            Flags = MessageFlags.Ephemeral
        };
    }

    /// <summary>"Confirm" button → removes the feed and refreshes the list. Payload: "{listMsgId}_{feedIdN}".</summary>
    [ComponentInteraction(CUSTOMID_DELETE_CONFIRM)]
    public async Task ConfirmDeleteAsync(string payload)
    {
        var locale = Context.Interaction.UserLocale;

        if (Context.Interaction.GuildId is not ulong guildId || !CanManage())
        {
            await ReplaceWithTextAsync(ResourceHelper.GetString(Strings.Exceptions.FeedManageRequired, locale));
            return;
        }

        var sepIdx = payload.IndexOf(CUSTOMID_INNER_SEP);
        if (sepIdx <= 0 || sepIdx == payload.Length - 1
            || !ulong.TryParse(payload[..sepIdx], out var listMsgId)
            || !Guid.TryParseExact(payload[(sepIdx + 1)..], "N", out var feedId))
        {
            await ReplaceWithTextAsync(ResourceHelper.GetString(Strings.Exceptions.InvalidParams, locale));
            return;
        }

        var feedService = MainServiceProvider.GetRequiredService<FeedService>();
        try
        {
            await feedService.RemoveFeedAsync(guildId, feedId);
        }
        catch (SlashCommandBusinessException)
        {
            await ReplaceWithTextAsync(ResourceHelper.GetString(Strings.Exceptions.FeedNotFound, locale));
            return;
        }

        // listMsgId == 0 means the picker was opened from /feed delete (no list message to refresh).
        if (listMsgId != 0)
            await RefreshListAsync(Context.Client.Rest, Context.Channel.Id, listMsgId, guildId, locale);
        await ReplaceWithTextAsync(ResourceHelper.GetString(Strings.Commands.FeedDeleted, locale));
    }

    /// <summary>"Cancel" button → drops the confirm prompt without deleting.</summary>
    [ComponentInteraction(CUSTOMID_DELETE_CANCEL)]
    public async Task CancelDeleteAsync() =>
        await ReplaceWithTextAsync(
            ResourceHelper.GetString(Strings.Commands.TriggerDeleteCancelled, Context.Interaction.UserLocale));

    /// <summary>Best-effort refresh of the public /feed list message after an add/delete.</summary>
    internal static async Task RefreshListAsync(RestClient rest, ulong channelId, ulong messageId, ulong guildId, string? locale)
    {
        try
        {
            var feedService = MainServiceProvider.GetRequiredService<FeedService>();
            var feeds = (await feedService.GetFeedsAsync(guildId)).OrderBy(f => f.CreatedAt).ToList();

            await rest.ModifyMessageAsync(channelId, messageId, m =>
            {
                m.Embeds = new[] { FeedListView.BuildEmbed(feeds, locale) };
                m.Components = FeedListView.BuildComponents(feeds, locale);
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to refresh /feed list message {MessageId} in channel {ChannelId}", messageId, channelId);
        }
    }

    private bool CanManage() =>
        Context.Interaction.User is GuildInteractionUser gu && gu.Permissions.HasFlag(Permissions.ManageMessages);

    private async Task RespondEphemeralAsync(string content) =>
        await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties
        {
            Content = content,
            Flags = MessageFlags.Ephemeral
        }));

    private async Task ReplaceWithTextAsync(string content) =>
        await Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(m =>
        {
            m.Content = content;
            m.Components = Array.Empty<IMessageComponentProperties>();
        }));

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";
}

/// <summary>
/// Select-menu half of the /feed delete widget: turns the chosen feed into a Confirm/Cancel step.
/// </summary>
public class FeedDeleteMenu : ComponentInteractionModule<StringMenuInteractionContext>
{
    [ComponentInteraction(FeedWidgetButtons.CUSTOMID_DELETE_SELECT)]
    public async Task OnSelectAsync(string listMsgIdStr)
    {
        var locale = Context.Interaction.UserLocale;

        if (Context.Interaction.GuildId is null
            || Context.Interaction.User is not GuildInteractionUser gu
            || !gu.Permissions.HasFlag(Permissions.ManageMessages))
        {
            await ReplaceWithTextAsync(ResourceHelper.GetString(Strings.Exceptions.FeedManageRequired, locale));
            return;
        }

        var selected = Context.SelectedValues.FirstOrDefault();
        if (selected is null || !Guid.TryParseExact(selected, "N", out var feedId))
        {
            await ReplaceWithTextAsync(ResourceHelper.GetString(Strings.Exceptions.InvalidParams, locale));
            return;
        }

        var feedService = MainServiceProvider.GetRequiredService<FeedService>();
        var feed = (await feedService.GetFeedsAsync(Context.Interaction.GuildId.Value))
            .FirstOrDefault(f => f.FeedId == feedId);
        if (feed is null)
        {
            await ReplaceWithTextAsync(ResourceHelper.GetString(Strings.Exceptions.FeedNotFound, locale));
            return;
        }

        var confirmRow = new ActionRowProperties(new IActionRowComponentProperties[]
        {
            new ButtonProperties(
                $"{FeedWidgetButtons.CUSTOMID_DELETE_CONFIRM}{FeedWidgetButtons.CUSTOMID_SEP}{listMsgIdStr}{FeedWidgetButtons.CUSTOMID_INNER_SEP}{feedId:N}",
                ResourceHelper.GetString(Strings.Commands.TriggerBtnConfirm, locale),
                ButtonStyle.Danger),
            new ButtonProperties(
                FeedWidgetButtons.CUSTOMID_DELETE_CANCEL,
                ResourceHelper.GetString(Strings.Commands.TriggerBtnCancel, locale),
                ButtonStyle.Secondary)
        });

        await Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(m =>
        {
            m.Content = ResourceHelper.GetString(Strings.Commands.FeedDeleteConfirmPrompt, locale, feed.Topic);
            m.Components = new IMessageComponentProperties[] { confirmRow };
        }));
    }

    private async Task ReplaceWithTextAsync(string content) =>
        await Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(m =>
        {
            m.Content = content;
            m.Components = Array.Empty<IMessageComponentProperties>();
        }));
}

/// <summary>
/// Modal half of the /feed add widget. Parses/validates the form, creates the feed via
/// FeedService (which validates the subreddit and enforces the per-guild limit), then
/// refreshes the list message.
/// </summary>
public class FeedAddModal : ComponentInteractionModule<ModalInteractionContext>
{
    [ComponentInteraction(FeedWidgetButtons.CUSTOMID_ADD_MODAL)]
    public async Task SubmitAsync(string listMsgIdStr)
    {
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));

        var locale = Context.Interaction.UserLocale;

        if (Context.Interaction.GuildId is not ulong guildId
            || Context.Interaction.User is not GuildInteractionUser gu
            || !gu.Permissions.HasFlag(Permissions.ManageMessages))
            throw new SlashCommandBusinessException(Strings.Exceptions.FeedManageRequired);

        string topic = (GetInput(FeedWidgetButtons.ADD_FIELD_TOPIC) ?? "").Trim();
        string intervalRaw = (GetInput(FeedWidgetButtons.ADD_FIELD_INTERVAL) ?? "").Trim();
        string startAtRaw = (GetInput(FeedWidgetButtons.ADD_FIELD_STARTAT) ?? "").Trim();
        string nsfwRaw = (GetInput(FeedWidgetButtons.ADD_FIELD_NSFW) ?? "").Trim();

        if (topic.Length is < FeedWidgetButtons.TOPIC_MIN or > FeedWidgetButtons.TOPIC_MAX)
            throw new SlashCommandBusinessException(Strings.Exceptions.InvalidParams);

        if (!int.TryParse(intervalRaw, out int interval)
            || interval is < FeedWidgetButtons.INTERVAL_MIN or > FeedWidgetButtons.INTERVAL_MAX)
            throw new SlashCommandBusinessException(Strings.Exceptions.InvalidParams);

        int startAt = -1;
        if (!string.IsNullOrWhiteSpace(startAtRaw)
            && (!int.TryParse(startAtRaw, out startAt)
                || startAt is < FeedWidgetButtons.START_AT_MIN or > FeedWidgetButtons.START_AT_MAX))
            throw new SlashCommandBusinessException(Strings.Exceptions.InvalidParams);

        bool allowNsfw = ParseYesNo(nsfwRaw);

        var feedService = MainServiceProvider.GetRequiredService<FeedService>();
        try
        {
            await feedService.CreateNewFeedAsync(
                new FeedProperties(guildId, Context.Channel.Id, topic, interval, startAt, allowNsfw));
        }
        catch (ArgumentException ex)
        {
            // Subreddit validation failures from FeedService/FeedContentService.
            throw new SlashCommandBusinessException(ex.Message);
        }

        // listMsgId == 0 means the modal was opened from /feed add (no list message to refresh).
        if (ulong.TryParse(listMsgIdStr, out var listMsgId) && listMsgId != 0)
            await FeedWidgetButtons.RefreshListAsync(Context.Client.Rest, Context.Channel.Id, listMsgId, guildId, locale);

        await Context.Interaction.SendFollowupMessageAsync(new InteractionMessageProperties
        {
            Content = ResourceHelper.GetString(Strings.Commands.FeedCreated, locale, topic),
            Flags = MessageFlags.Ephemeral
        });
    }

    private static bool ParseYesNo(string raw) =>
        raw.ToLowerInvariant() is "yes" or "y" or "oui" or "o" or "true" or "1";

    private string? GetInput(string customId) =>
        Context.Components
            .OfType<Label>()
            .Select(l => l.Component)
            .OfType<TextInput>()
            .FirstOrDefault(ti => ti.CustomId == customId)?.Value;
}
