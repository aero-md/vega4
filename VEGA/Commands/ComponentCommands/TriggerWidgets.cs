using System.Text.RegularExpressions;
using Core;
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
/// Shared renderer for the /trigger list message (embed + Add/Delete buttons).
/// Centralized so the slash command and the widgets all produce the same view,
/// which makes refreshing the message after an add/delete a single edit.
/// </summary>
public static class TriggerListView
{
    private const int FIELD_RESPONSE_MAX = 900; // keep each field value under Discord's 1024-char limit

    public static EmbedProperties BuildEmbed(IReadOnlyList<Trigger> triggers, string? locale)
    {
        locale ??= "en-US";
        var embed = new EmbedProperties
        {
            // CurrentTriggersHeader carries a markdown "## " prefix meant for plain messages — strip it for an embed title.
            Title = ResourceHelper.GetString(Strings.Commands.CurrentTriggersHeader, locale).TrimStart('#', ' ')
        };

        if (triggers.Count == 0)
        {
            embed.Description = ResourceHelper.GetString(Strings.Commands.NoTriggersOnServer, locale);
            return embed;
        }

        // One field per trigger: name = "1.", "2."… ; value = pattern / response / small options line.
        var fields = new List<EmbedFieldProperties>();
        for (int i = 0; i < triggers.Count; i++)
        {
            var t = triggers[i];
            fields.Add(new EmbedFieldProperties
            {
                Name = $"{i + 1}.",
                Value = ResourceHelper.GetString(
                    Strings.Commands.TriggerInfo, locale,
                    t.Pattern, Truncate(t.Response, FIELD_RESPONSE_MAX), t.RegexOptions, t.PingOnReply)
            });
        }

        embed.Fields = fields;
        return embed;
    }

    public static IEnumerable<IMessageComponentProperties> BuildComponents(IReadOnlyList<Trigger> triggers, string? locale)
    {
        locale ??= "en-US";
        var buttons = new List<IActionRowComponentProperties>
        {
            new ButtonProperties(
                TriggerWidgetButtons.CUSTOMID_ADD_OPEN,
                ResourceHelper.GetString(Strings.Commands.TriggerBtnAdd, locale),
                ButtonStyle.Success)
        };

        // Delete only makes sense when there is something to delete.
        if (triggers.Count > 0)
        {
            buttons.Add(new ButtonProperties(
                TriggerWidgetButtons.CUSTOMID_DELETE_OPEN,
                ResourceHelper.GetString(Strings.Commands.TriggerBtnDelete, locale),
                ButtonStyle.Danger));
        }

        return new IMessageComponentProperties[] { new ActionRowProperties(buttons) };
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";
}

/// <summary>
/// Button half of the /trigger management widget.
///   /trigger list  → [➕ Add] (CUSTOMID_ADD_OPEN) and [🗑️ Delete] (CUSTOMID_DELETE_OPEN)
///   ➕ → modal (TriggerAddModal) ; 🗑️ → select menu (TriggerDeleteMenu) → Confirm/Cancel.
/// The originating list message id is threaded through the custom_ids so the public list
/// message can be refreshed once the action completes.
/// Prefixes must not contain CUSTOMID_SEP (':'), NetCord's parameter separator.
/// </summary>
public class TriggerWidgetButtons : ComponentInteractionModule<ButtonInteractionContext>
{
    public const string CUSTOMID_ADD_OPEN = "trigger_addopen";
    public const string CUSTOMID_ADD_MODAL = "trigger_addmodal";
    public const string CUSTOMID_DELETE_OPEN = "trigger_delopen";
    public const string CUSTOMID_DELETE_SELECT = "trigger_delselect";
    public const string CUSTOMID_DELETE_CONFIRM = "trigger_delconfirm";
    public const string CUSTOMID_DELETE_CANCEL = "trigger_delcancel";
    public const char CUSTOMID_SEP = ':';
    public const char CUSTOMID_INNER_SEP = '_';
    // Sentinel used when the widget is opened directly from a slash command (no list message to refresh).
    public const string NO_LIST = "0";

    // Modal text-input ids (internal to the modal, not routed by [ComponentInteraction]).
    public const string ADD_FIELD_REGEX = "trigger_add_regex";
    public const string ADD_FIELD_RESPONSE = "trigger_add_response";
    public const string ADD_FIELD_OPTIONS = "trigger_add_options";

    public const int REGEX_MIN_LENGTH = 3;
    public const int REGEX_MAX_LENGTH = 50;
    public const int RESPONSE_MIN_LENGTH = 1;
    public const int RESPONSE_MAX_LENGTH = 2000;

    private const int MAX_MENU_OPTIONS = 25;   // Discord cap; per-guild trigger cap is well below
    private const int OPTION_TEXT_MAX = 100;   // Discord cap on option label/description

    /// <summary>"➕ Add" button → opens the creation modal (carrying the list message id).</summary>
    [ComponentInteraction(CUSTOMID_ADD_OPEN)]
    public async Task OpenAddModalAsync()
    {
        var locale = Context.Interaction.UserLocale ?? "en-US";

        if (Context.Interaction.GuildId is null || !CanManage())
        {
            await RespondEphemeralAsync(ResourceHelper.GetString(Strings.Exceptions.TriggerManageRequired, locale));
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
            await RespondEphemeralAsync(ResourceHelper.GetString(Strings.Exceptions.TriggerManageRequired, locale));
            return;
        }

        var service = MainServiceProvider.GetRequiredService<GuildSettingsService>();
        var settings = await service.GetByIdAsync(guildId);

        await Context.Interaction.SendResponseAsync(
            InteractionCallback.Message(BuildDeleteMenuMessage(settings.Triggers, Context.Message.Id.ToString(), locale)));
    }

    /// <summary>
    /// Builds the trigger-creation modal. <paramref name="listMsgId"/> threads the /trigger list
    /// message to refresh on submit (use <see cref="NO_LIST"/> when opened directly via /trigger add).
    /// </summary>
    public static ModalProperties BuildAddModal(string listMsgId, string locale) =>
        new ModalProperties(
            $"{CUSTOMID_ADD_MODAL}{CUSTOMID_SEP}{listMsgId}",
            ResourceHelper.GetString(Strings.Commands.TriggerAddModalTitle, locale),
            new IModalComponentProperties[]
            {
                new LabelProperties(
                    ResourceHelper.GetString(Strings.Commands.TriggerAddFieldRegex, locale),
                    new TextInputProperties(ADD_FIELD_REGEX, TextInputStyle.Short)
                    { Required = true, MinLength = REGEX_MIN_LENGTH, MaxLength = REGEX_MAX_LENGTH }),
                new LabelProperties(
                    ResourceHelper.GetString(Strings.Commands.TriggerAddFieldResponse, locale),
                    new TextInputProperties(ADD_FIELD_RESPONSE, TextInputStyle.Paragraph)
                    { Required = true, MinLength = RESPONSE_MIN_LENGTH, MaxLength = RESPONSE_MAX_LENGTH }),
                new LabelProperties(
                    ResourceHelper.GetString(Strings.Commands.TriggerAddFieldOptions, locale),
                    new TextInputProperties(ADD_FIELD_OPTIONS, TextInputStyle.Short)
                    { Required = false, MaxLength = 10 })
            });

    /// <summary>
    /// Builds the ephemeral delete picker (or a "no triggers" message). <paramref name="listMsgId"/>
    /// is threaded for refresh (use <see cref="NO_LIST"/> when opened directly via /trigger delete).
    /// </summary>
    public static InteractionMessageProperties BuildDeleteMenuMessage(IEnumerable<Trigger> allTriggers, string listMsgId, string locale)
    {
        // Same order as the list (oldest first) so menu positions match what the user sees.
        var triggers = allTriggers.OrderBy(t => t.CreatedAt).Take(MAX_MENU_OPTIONS).ToList();

        if (triggers.Count == 0)
            return new InteractionMessageProperties
            {
                Content = ResourceHelper.GetString(Strings.Commands.NoTriggersOnServer, locale),
                Flags = MessageFlags.Ephemeral
            };

        var options = triggers.Select(t =>
        {
            var opt = new StringMenuSelectOptionProperties(Truncate(t.Pattern, OPTION_TEXT_MAX), t.TriggerId.ToString("N"));
            var desc = Truncate(t.Response, OPTION_TEXT_MAX);
            if (!string.IsNullOrWhiteSpace(desc))
                opt.Description = desc;
            return opt;
        });

        var menu = new StringMenuProperties($"{CUSTOMID_DELETE_SELECT}{CUSTOMID_SEP}{listMsgId}", options)
        {
            Placeholder = ResourceHelper.GetString(Strings.Commands.TriggerDeleteMenuPlaceholder, locale),
            MinValues = 1,
            MaxValues = 1
        };

        return new InteractionMessageProperties
        {
            Content = ResourceHelper.GetString(Strings.Commands.TriggerDeleteMenuPrompt, locale),
            Components = new IMessageComponentProperties[] { menu },
            Flags = MessageFlags.Ephemeral
        };
    }

    /// <summary>"Confirm" button → deletes the trigger and refreshes the list. Payload: "{listMsgId}_{uuidN}".</summary>
    [ComponentInteraction(CUSTOMID_DELETE_CONFIRM)]
    public async Task ConfirmDeleteAsync(string payload)
    {
        var locale = Context.Interaction.UserLocale;

        if (Context.Interaction.GuildId is not ulong guildId || !CanManage())
        {
            await ReplaceWithTextAsync(ResourceHelper.GetString(Strings.Exceptions.TriggerManageRequired, locale));
            return;
        }

        var sepIdx = payload.IndexOf(CUSTOMID_INNER_SEP);
        if (sepIdx <= 0 || sepIdx == payload.Length - 1
            || !ulong.TryParse(payload[..sepIdx], out var listMsgId)
            || !Guid.TryParseExact(payload[(sepIdx + 1)..], "N", out var triggerId))
        {
            await ReplaceWithTextAsync(ResourceHelper.GetString(Strings.Exceptions.InvalidParams, locale));
            return;
        }

        var service = MainServiceProvider.GetRequiredService<GuildSettingsService>();
        string? pattern = await service.DeleteTrigger(guildId, triggerId);

        // listMsgId == 0 means the picker was opened from /trigger delete (no list message to refresh).
        if (pattern != null && listMsgId != 0)
            await RefreshListAsync(Context.Client.Rest, Context.Channel.Id, listMsgId, guildId, locale);

        await ReplaceWithTextAsync(pattern != null
            ? ResourceHelper.GetString(Strings.Commands.TriggerDeleted, locale, pattern)
            : ResourceHelper.GetString(Strings.Exceptions.TriggerNotFound, locale));
    }

    /// <summary>"Cancel" button → drops the confirm prompt without deleting.</summary>
    [ComponentInteraction(CUSTOMID_DELETE_CANCEL)]
    public async Task CancelDeleteAsync() =>
        await ReplaceWithTextAsync(
            ResourceHelper.GetString(Strings.Commands.TriggerDeleteCancelled, Context.Interaction.UserLocale));

    /// <summary>
    /// Best-effort refresh of the public /trigger list message after an add/delete.
    /// Failures (message deleted, missing perms…) are logged but never break the action.
    /// </summary>
    internal static async Task RefreshListAsync(RestClient rest, ulong channelId, ulong messageId, ulong guildId, string? locale)
    {
        try
        {
            var service = MainServiceProvider.GetRequiredService<GuildSettingsService>();
            var settings = await service.GetByIdAsync(guildId);
            // Ascending so a freshly-added trigger appears at the end of the refreshed list.
            var triggers = settings.Triggers.OrderBy(t => t.CreatedAt).ToList();

            await rest.ModifyMessageAsync(channelId, messageId, m =>
            {
                m.Embeds = new[] { TriggerListView.BuildEmbed(triggers, locale) };
                m.Components = TriggerListView.BuildComponents(triggers, locale);
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to refresh /trigger list message {MessageId} in channel {ChannelId}", messageId, channelId);
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

    // Edits the ephemeral widget message in place, stripping its components.
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
/// Select-menu half of the /trigger delete widget: turns the chosen trigger into a
/// Confirm/Cancel step (in place, same ephemeral message). The list message id received
/// from the menu custom_id is carried over to the Confirm button.
/// </summary>
public class TriggerDeleteMenu : ComponentInteractionModule<StringMenuInteractionContext>
{
    [ComponentInteraction(TriggerWidgetButtons.CUSTOMID_DELETE_SELECT)]
    public async Task OnSelectAsync(string listMsgIdStr)
    {
        var locale = Context.Interaction.UserLocale;

        if (Context.Interaction.GuildId is null
            || Context.Interaction.User is not GuildInteractionUser gu
            || !gu.Permissions.HasFlag(Permissions.ManageMessages))
        {
            await ReplaceWithTextAsync(ResourceHelper.GetString(Strings.Exceptions.TriggerManageRequired, locale));
            return;
        }

        var selected = Context.SelectedValues.FirstOrDefault();
        if (selected is null || !Guid.TryParseExact(selected, "N", out var triggerId))
        {
            await ReplaceWithTextAsync(ResourceHelper.GetString(Strings.Exceptions.InvalidParams, locale));
            return;
        }

        var service = MainServiceProvider.GetRequiredService<GuildSettingsService>();
        var settings = await service.GetByIdAsync(Context.Interaction.GuildId.Value);
        var trigger = settings.Triggers.FirstOrDefault(t => t.TriggerId == triggerId);
        if (trigger is null)
        {
            await ReplaceWithTextAsync(ResourceHelper.GetString(Strings.Exceptions.TriggerNotFound, locale));
            return;
        }

        var confirmRow = new ActionRowProperties(new IActionRowComponentProperties[]
        {
            new ButtonProperties(
                $"{TriggerWidgetButtons.CUSTOMID_DELETE_CONFIRM}{TriggerWidgetButtons.CUSTOMID_SEP}{listMsgIdStr}{TriggerWidgetButtons.CUSTOMID_INNER_SEP}{triggerId:N}",
                ResourceHelper.GetString(Strings.Commands.TriggerBtnConfirm, locale),
                ButtonStyle.Danger),
            new ButtonProperties(
                TriggerWidgetButtons.CUSTOMID_DELETE_CANCEL,
                ResourceHelper.GetString(Strings.Commands.TriggerBtnCancel, locale),
                ButtonStyle.Secondary)
        });

        await Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(m =>
        {
            m.Content = ResourceHelper.GetString(Strings.Commands.TriggerDeleteConfirmPrompt, locale, trigger.Pattern);
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
/// Modal half of the /trigger add widget. Validates the same way the old slash subcommand did
/// (length bounds + TriggerRegex), creates the trigger, then refreshes the list message.
/// </summary>
public class TriggerAddModal : ComponentInteractionModule<ModalInteractionContext>
{
    [ComponentInteraction(TriggerWidgetButtons.CUSTOMID_ADD_MODAL)]
    public async Task SubmitAsync(string listMsgIdStr)
    {
        // Defer ephemeral so business errors can be thrown and surfaced by the dispatcher.
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));

        var locale = Context.Interaction.UserLocale;

        if (Context.Interaction.GuildId is not ulong guildId
            || Context.Interaction.User is not GuildInteractionUser gu
            || !gu.Permissions.HasFlag(Permissions.ManageMessages))
            throw new SlashCommandBusinessException(Strings.Exceptions.TriggerManageRequired);

        string regex = GetInput(TriggerWidgetButtons.ADD_FIELD_REGEX) ?? "";
        string response = GetInput(TriggerWidgetButtons.ADD_FIELD_RESPONSE) ?? "";
        string optionsRaw = GetInput(TriggerWidgetButtons.ADD_FIELD_OPTIONS) ?? "";

        if (regex.Length is < TriggerWidgetButtons.REGEX_MIN_LENGTH or > TriggerWidgetButtons.REGEX_MAX_LENGTH
            || response.Length is < TriggerWidgetButtons.RESPONSE_MIN_LENGTH or > TriggerWidgetButtons.RESPONSE_MAX_LENGTH)
            throw new SlashCommandBusinessException(Strings.Exceptions.InvalidParams);

        int regexOptions = 0;
        if (!string.IsNullOrWhiteSpace(optionsRaw) && !int.TryParse(optionsRaw, out regexOptions))
            throw new SlashCommandBusinessException(Strings.Exceptions.InvalidParams);

        // Reject invalid syntax / obvious ReDoS up front.
        try
        {
            TriggerRegex.Validate(regex, regexOptions);
        }
        catch (ArgumentException ex)
        {
            throw new SlashCommandBusinessException(Strings.Exceptions.InvalidRegexPattern, ex.Message);
        }
        catch (RegexMatchTimeoutException)
        {
            throw new SlashCommandBusinessException(Strings.Exceptions.InvalidRegexPattern, "timeout");
        }

        int sanitizedOptions = (int)TriggerRegex.Sanitize(regexOptions);

        var service = MainServiceProvider.GetRequiredService<GuildSettingsService>();
        await service.AddTrigger(guildId, new Trigger(guildId, regex, response, sanitizedOptions));

        // listMsgId == 0 means the modal was opened from /trigger add (no list message to refresh).
        if (ulong.TryParse(listMsgIdStr, out var listMsgId) && listMsgId != 0)
            await TriggerWidgetButtons.RefreshListAsync(Context.Client.Rest, Context.Channel.Id, listMsgId, guildId, locale);

        await Context.Interaction.SendFollowupMessageAsync(new InteractionMessageProperties
        {
            Content = ResourceHelper.GetString(Strings.Commands.TriggerAdded, locale, regex, response),
            Flags = MessageFlags.Ephemeral
        });
    }

    private string? GetInput(string customId) =>
        Context.Components
            .OfType<Label>()
            .Select(l => l.Component)
            .OfType<TextInput>()
            .FirstOrDefault(ti => ti.CustomId == customId)?.Value;
}
