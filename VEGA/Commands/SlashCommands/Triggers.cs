using ComponentCommands;
using Core.CustomCommandAttributes;
using Microsoft.Extensions.DependencyInjection;
using Models.Entities;
using NetCord;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using Services;
using static Core.GlobalRegistry;

namespace SlashCommands;

[SlashCommand("trigger", "Manage triggers patterns for this server")]
public class Triggers : ApplicationCommandModule<ApplicationCommandContext>
{
    // Adding and deleting triggers is handled entirely through the /trigger list widget
    // (➕ Add → modal, 🗑️ Delete → select menu + confirm), see TriggerWidgetButtons /
    // TriggerDeleteMenu / TriggerAddModal. Those flows refresh this very message in place.
    [DefferedResponse]
    [SubSlashCommand("list", "List triggers on this server, and add or delete them")]
    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    public async Task List()
    {
        GuildSettingsService service = MainServiceProvider.GetRequiredService<GuildSettingsService>();
        GuildSettings settings = await service.GetByIdAsync(Context.Interaction.Guild!.Id);

        // Ascending: oldest first, newly-added triggers append at the bottom of the list.
        var triggers = settings.Triggers.OrderBy(x => x.CreatedAt).ToList();
        var locale = Context.Interaction.UserLocale;

        await Context.Interaction.SendFollowupMessageAsync(new InteractionMessageProperties
        {
            Embeds = new[] { TriggerListView.BuildEmbed(triggers, locale) },
            Components = TriggerListView.BuildComponents(triggers, locale)
        });
    }

    // Shortcut: open the add form directly (same modal as the /trigger list ➕ button).
    // No [DefferedResponse]: a modal must be the immediate interaction response.
    [SubSlashCommand("add", "Add a trigger (opens a form)")]
    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [RequireUserPermissions<ApplicationCommandContext>(Permissions.ManageMessages)]
    public async Task Add()
    {
        var locale = Context.Interaction.UserLocale ?? "en-US";
        await Context.Interaction.SendResponseAsync(
            InteractionCallback.Modal(TriggerWidgetButtons.BuildAddModal(TriggerWidgetButtons.NO_LIST, locale)));
    }

    // Shortcut: open the delete picker directly (same select menu as the 🗑️ button).
    [SubSlashCommand("delete", "Delete a trigger (opens a picker)")]
    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [RequireUserPermissions<ApplicationCommandContext>(Permissions.ManageMessages)]
    public async Task Delete()
    {
        var locale = Context.Interaction.UserLocale ?? "en-US";
        var service = MainServiceProvider.GetRequiredService<GuildSettingsService>();
        var settings = await service.GetByIdAsync(Context.Interaction.Guild!.Id);

        await Context.Interaction.SendResponseAsync(
            InteractionCallback.Message(
                TriggerWidgetButtons.BuildDeleteMenuMessage(settings.Triggers, TriggerWidgetButtons.NO_LIST, locale)));
    }
}
