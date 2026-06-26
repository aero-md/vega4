using Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Models.Core;
using Models.Entities;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;
using Resources;
using Services;
using static Core.GlobalRegistry;

namespace ComponentCommands;

/// <summary>
/// Submit handler for the /feedconfig edit modal. Single-row global config, so no payload
/// in the custom_id. Component interactions bypass the slash-command [RequireSuperAdmin]
/// gate, so superadmin is re-checked here.
/// </summary>
public class FeedConfigModal : ComponentInteractionModule<ModalInteractionContext>
{
    public const string CUSTOMID = "feedconfig_modal";

    public const string FIELD_FETCH_SIZE = "fc_fetch_size";
    public const string FIELD_FETCH_INTERVAL = "fc_fetch_interval";
    public const string FIELD_HISTORY_SIZE = "fc_history_size";
    public const string FIELD_SORT_MODE = "fc_sort_mode";
    public const string FIELD_MAX_FEEDS = "fc_max_feeds";

    [ComponentInteraction(CUSTOMID)]
    public async Task SubmitAsync()
    {
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));

        var locale = Context.Interaction.UserLocale ?? "en-US";

        // Re-check superadmin: the slash gate doesn't apply to component/modal interactions.
        var vegaConfig = MainServiceProvider.GetRequiredService<VegaConfiguration>();
        if (!vegaConfig.SuperAdminUserIds.Contains(Context.Interaction.User.Id))
            throw new SlashCommandBusinessException(Strings.Exceptions.RequireSuperAdmin);

        var feedContentService = MainServiceProvider.GetRequiredService<FeedContentService>();
        var config = await feedContentService.GetConfigAsync();

        config.FetchSize = ParseInt(FIELD_FETCH_SIZE, "fetch_size", "1-100", locale, v => v is >= 1 and <= 100);
        config.FetchIntervalMinutes = ParseInt(FIELD_FETCH_INTERVAL, "fetch_interval_minutes", "≥ 1", locale, v => v >= 1);
        config.HistorySize = ParseInt(FIELD_HISTORY_SIZE, "history_size", "≥ 1", locale, v => v >= 1);
        config.MaxFeedsPerGuild = ParseInt(FIELD_MAX_FEEDS, "max_feeds_per_guild", "≥ 1", locale, v => v >= 1);

        var sortMode = (GetSelect(FIELD_SORT_MODE) ?? "").Trim().ToLowerInvariant();
        var validModes = FeedConfiguration.GetValidSortModes();
        if (!validModes.Contains(sortMode))
            throw new SlashCommandBusinessException(
                ResourceHelper.GetString(Strings.Exceptions.FeedConfigInvalidValue, locale, "sort_mode", string.Join(", ", validModes)));
        config.SortMode = sortMode;

        await feedContentService.UpdateConfigAsync(config);

        await Context.Interaction.SendFollowupMessageAsync(new InteractionMessageProperties
        {
            Content = ResourceHelper.GetString(Strings.Commands.FeedConfigSaved, locale),
            Flags = MessageFlags.Ephemeral
        });
    }

    private int ParseInt(string fieldId, string name, string expected, string locale, Func<int, bool> valid)
    {
        if (!int.TryParse((GetInput(fieldId) ?? "").Trim(), out var value) || !valid(value))
            throw new SlashCommandBusinessException(
                ResourceHelper.GetString(Strings.Exceptions.FeedConfigInvalidValue, locale, name, expected));
        return value;
    }

    private string? GetInput(string customId) =>
        Context.Components
            .OfType<Label>()
            .Select(l => l.Component)
            .OfType<TextInput>()
            .FirstOrDefault(ti => ti.CustomId == customId)?.Value;

    private string? GetSelect(string customId)
    {
        var menu = Context.Components
            .OfType<Label>()
            .Select(l => l.Component)
            .OfType<StringMenu>()
            .FirstOrDefault(sm => sm.CustomId == customId);
        return menu?.SelectedValues is { Count: > 0 } values ? values[0] : null;
    }
}
