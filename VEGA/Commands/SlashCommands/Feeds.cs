using static Core.GlobalRegistry;
using NetCord;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using Microsoft.Extensions.DependencyInjection;
using Services;
using Core.CustomCommandAttributes;
using ComponentCommands;

namespace SlashCommands;

[SlashCommand("feed", "Manage feeds for this server")]
public class Feeds : ApplicationCommandModule<ApplicationCommandContext>
{
    // Creating and deleting feeds is handled through the /feed list widget
    // (➕ Add → modal, 🗑️ Delete → select menu + confirm), see FeedWidgetButtons /
    // FeedDeleteMenu / FeedAddModal. Those flows refresh this very message in place.
    [DefferedResponse]
    [SubSlashCommand("list", "List feeds on this server, and add or delete them")]
    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [RequireUserPermissions<ApplicationCommandContext>(Permissions.ManageMessages)]
    public async Task ListFeeds()
    {
        var feedService = MainServiceProvider.GetRequiredService<FeedService>();
        // Ascending so newly-added feeds append at the bottom of the list.
        var feeds = (await feedService.GetFeedsAsync(Context.Interaction.Guild!.Id))
            .OrderBy(f => f.CreatedAt)
            .ToList();
        var locale = Context.Interaction.UserLocale;

        await Context.Interaction.SendFollowupMessageAsync(new InteractionMessageProperties
        {
            Embeds = new[] { FeedListView.BuildEmbed(feeds, locale) },
            Components = FeedListView.BuildComponents(feeds, locale)
        });
    }

    // Shortcut: open the add form directly (same modal as the /feed list ➕ button).
    // No [DefferedResponse]: a modal must be the immediate interaction response.
    [SubSlashCommand("add", "Add a feed (opens a form)")]
    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [RequireUserPermissions<ApplicationCommandContext>(Permissions.ManageMessages)]
    public async Task Add()
    {
        var locale = Context.Interaction.UserLocale ?? "en-US";
        await Context.Interaction.SendResponseAsync(
            InteractionCallback.Modal(FeedWidgetButtons.BuildAddModal(FeedWidgetButtons.NO_LIST, locale)));
    }

    // Shortcut: open the delete picker directly (same select menu as the 🗑️ button).
    [SubSlashCommand("delete", "Delete a feed (opens a picker)")]
    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [RequireUserPermissions<ApplicationCommandContext>(Permissions.ManageMessages)]
    public async Task Delete()
    {
        var locale = Context.Interaction.UserLocale ?? "en-US";
        var feedService = MainServiceProvider.GetRequiredService<FeedService>();
        var feeds = await feedService.GetFeedsAsync(Context.Interaction.Guild!.Id);

        await Context.Interaction.SendResponseAsync(
            InteractionCallback.Message(
                FeedWidgetButtons.BuildDeleteMenuMessage(feeds, FeedWidgetButtons.NO_LIST, locale)));
    }
}
