using static Core.GlobalRegistry;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Microsoft.Extensions.DependencyInjection;
using Services;
using Core.CustomCommandAttributes;
using Resources;
using Models.Entities;
using ComponentCommands;

namespace SlashCommands;

[BackofficeCommand]
public class FeedConfig : ApplicationCommandModule<ApplicationCommandContext>
{
    // No [DefferedResponse]: a modal must be the immediate interaction response.
    // [RequireSuperAdmin] is enforced by CommandInteractionHandler before this runs; the
    // modal submit re-checks it (component interactions bypass that gate) — see FeedConfigModal.
    [RequireSuperAdmin]
    [SlashCommand("feedconfig", "Edit feed system configuration (SuperAdmin only)")]
    public async Task OpenConfig()
    {
        var config = await MainServiceProvider.GetRequiredService<FeedContentService>().GetConfigAsync();
        var locale = Context.Interaction.UserLocale;

        var modal = new ModalProperties(
            FeedConfigModal.CUSTOMID,
            ResourceHelper.GetString(Strings.Commands.FeedConfigModalTitle, locale),
            new IModalComponentProperties[]
            {
                Field("fetch_size", FeedConfigModal.FIELD_FETCH_SIZE, config.FetchSize.ToString(), "1-100"),
                Field("fetch_interval_minutes", FeedConfigModal.FIELD_FETCH_INTERVAL, config.FetchIntervalMinutes.ToString(), "≥ 1"),
                Field("history_size", FeedConfigModal.FIELD_HISTORY_SIZE, config.HistorySize.ToString(), "≥ 1"),
                new LabelProperties("sort_mode", new StringMenuProperties(
                    FeedConfigModal.FIELD_SORT_MODE,
                    FeedConfiguration.GetValidSortModes()
                        .Select(m => new StringMenuSelectOptionProperties(m, m) { Default = m == config.SortMode }))
                {
                    MinValues = 1,
                    MaxValues = 1
                }),
                Field("max_feeds_per_guild", FeedConfigModal.FIELD_MAX_FEEDS, config.MaxFeedsPerGuild.ToString(), "≥ 1"),
            });

        await Context.Interaction.SendResponseAsync(InteractionCallback.Modal(modal));
    }

    // Pre-filled labeled text input (label = the raw config key name, value = current setting).
    private static LabelProperties Field(string label, string fieldId, string value, string placeholder) =>
        new LabelProperties(label, new TextInputProperties(fieldId, TextInputStyle.Short)
        {
            Required = true,
            Value = value,
            Placeholder = placeholder
        });
}
