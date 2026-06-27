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
/// each module method is responsible for its own response (defer, message or modify).
///
/// One overload per component context (buttons, string menus, …); they all funnel
/// into the generic <see cref="DispatchAsync"/> core.
/// </summary>
public static class ComponentInteractionHandler
{
    public static Task HandleAsync(
        GatewayClient client,
        ComponentInteractionService<ButtonInteractionContext> componentService,
        MessageComponentInteraction interaction)
    {
        if (interaction is not ButtonInteraction buttonInteraction)
        {
            Log.Warning("Unsupported component interaction type: {InteractionType} (customId={CustomId})",
                interaction.GetType().Name, interaction.Data.CustomId);
            return Task.CompletedTask;
        }

        return DispatchAsync(
            componentService,
            new ButtonInteractionContext(buttonInteraction, client),
            buttonInteraction,
            buttonInteraction.Data.CustomId);
    }

    public static Task HandleAsync(
        GatewayClient client,
        ComponentInteractionService<StringMenuInteractionContext> componentService,
        StringMenuInteraction interaction)
        => DispatchAsync(
            componentService,
            new StringMenuInteractionContext(interaction, client),
            interaction,
            interaction.Data.CustomId);

    public static Task HandleAsync(
        GatewayClient client,
        ComponentInteractionService<ModalInteractionContext> componentService,
        ModalInteraction interaction)
        => DispatchAsync(
            componentService,
            new ModalInteractionContext(interaction, client),
            interaction,
            interaction.Data.CustomId);

    private static async Task DispatchAsync<TContext>(
        ComponentInteractionService<TContext> service,
        TContext context,
        Interaction interaction,
        string customId)
        where TContext : IComponentInteractionContext
    {
        var locale = interaction.UserLocale;
        string? errorMsg = null;

        try
        {
            IExecutionResult result = await service.ExecuteAsync(context, MainServiceProvider);

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

        // Best-effort ephemeral error. Handlers that already responded get a followup;
        // if Discord rejects it (e.g. nothing was responded yet) we still log it.
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
