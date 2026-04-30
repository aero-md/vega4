using Exceptions;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ComponentInteractions;
using Resources;
using Serilog;
using static Core.GlobalRegistry;

namespace Handlers;

/// <summary>
/// Dispatches message component interactions to the matching method on a
/// ComponentInteractionService module (decorated with [ComponentInteraction]).
/// Mirrors the i18n + error-handling pattern of CommandInteractionHandler;
/// each module method is responsible for its own deferred response.
/// </summary>
public static class ComponentInteractionHandler
{
    public static async Task HandleAsync(
        GatewayClient client,
        ComponentInteractionService<ButtonInteractionContext> componentService,
        MessageComponentInteraction interaction)
    {
        // Today only buttons are wired up. Other component types (select menus,
        // modals…) would each get their own service+context and a sibling branch.
        if (interaction is not ButtonInteraction buttonInteraction)
        {
            Log.Warning("Unsupported component interaction type: {InteractionType} (customId={CustomId})",
                interaction.GetType().Name, interaction.Data.CustomId);
            return;
        }

        var customId = interaction.Data.CustomId;
        var locale = interaction.UserLocale;
        string? errorMsg = null;

        try
        {
            var ctx = new ButtonInteractionContext(buttonInteraction, client);
            IExecutionResult result = await componentService.ExecuteAsync(ctx, MainServiceProvider);

            switch (result)
            {
                case NotFoundResult:
                    // No [ComponentInteraction] handler matched this customId.
                    Log.Warning("Unhandled component customId: {CustomId}", customId);
                    return;

                case ExecutionExceptionResult exec:
                    throw exec.Exception;

                case IFailResult fail:
                    Log.Warning("Component interaction failed: {Message} (customId={CustomId})",
                        fail.Message, customId);
                    errorMsg = ResourceHelper.GetString(Strings.Exceptions.CommandExecutionFailed, locale);
                    break;
            }
        }
        catch (SlashCommandBusinessException bex)
        {
            errorMsg = ResourceHelper.GetString(bex.Message, locale, bex.Args ?? Array.Empty<object>());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected error handling component interaction {CustomId}", customId);
            errorMsg = ResourceHelper.GetString(Strings.Exceptions.CommandExecutionCritical, locale);
        }

        if (errorMsg == null)
            return;

        // The action method defers as its first step, so a followup is the right
        // shape after a successful match. Pre-execution failures (NotFound, etc.)
        // already returned above, so we shouldn't be in the never-deferred case;
        // if Discord rejects the followup we still log it.
        try
        {
            await interaction.SendFollowupMessageAsync(new InteractionMessageProperties
            {
                Content = errorMsg,
                Flags = MessageFlags.Ephemeral
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to send component interaction error response for {CustomId}", customId);
        }
    }
}
