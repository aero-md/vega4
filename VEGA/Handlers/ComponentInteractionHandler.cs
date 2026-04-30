using static Core.GlobalRegistry;
using Exceptions;
using MessageCommands;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using Resources;
using Serilog;

namespace Handlers;

/// <summary>
/// Routes message component interactions (buttons, select menus) to the right handler
/// based on the customId prefix.
/// Mirrors the i18n + deferred-response + error-handling pattern of CommandInteractionHandler.
/// </summary>
public static class ComponentInteractionHandler
{
    public static async Task HandleAsync(GatewayClient client, MessageComponentInteraction interaction)
    {
        var customId = interaction.Data.CustomId;
        var locale = interaction.UserLocale;

        // Dispatch — ephemeral choice depends on the action
        bool ephemeral = !customId.StartsWith(DownloadEmotes.CUSTOMID_ZIP_PREFIX);

        string? errorMsg = null;
        bool deferred = false;

        try
        {
            await interaction.SendResponseAsync(
                InteractionCallback.DeferredMessage(ephemeral ? MessageFlags.Ephemeral : null)
            );
            deferred = true;

            if (customId.StartsWith(DownloadEmotes.CUSTOMID_ZIP_PREFIX))
            {
                var widgetId = customId[DownloadEmotes.CUSTOMID_ZIP_PREFIX.Length..];
                await HandleEmoteZipAsync(interaction, widgetId);
            }
            else if (customId.StartsWith(DownloadEmotes.CUSTOMID_ADD_PREFIX))
            {
                var widgetId = customId[DownloadEmotes.CUSTOMID_ADD_PREFIX.Length..];
                await HandleEmoteAddToServerAsync(interaction, widgetId);
            }
            else
            {
                Log.Warning("Unhandled component customId: {CustomId}", customId);
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

        if (errorMsg != null && deferred)
        {
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


    #region Emote widget handlers

    private static async Task HandleEmoteZipAsync(MessageComponentInteraction interaction, string widgetId)
    {
        var state = LoadEmoteWidgetState(widgetId);
        EnsureInvoker(interaction, state);

        var httpClient = MainServiceProvider
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient(Core.HttpClientNames.Emotes);

        var downloaded = await DownloadEmotes.DownloadEmotesAsync(state.Emotes, httpClient);

        using var zipStream = await DownloadEmotes.BuildZipAsync(downloaded);

        var locale = interaction.UserLocale;
        await interaction.SendFollowupMessageAsync(new InteractionMessageProperties
        {
            Content = ResourceHelper.GetString(
                state.Emotes.Count > 1 ? Strings.Commands.DlEmotesResultMultiple : Strings.Commands.DlEmotesResultSingle,
                locale,
                state.Emotes.Count
            ),
            Attachments = new[]
            {
                new AttachmentProperties(DownloadEmotes.EMOTE_ZIPFILE_NAME, zipStream)
            }
        });
    }

    private static async Task HandleEmoteAddToServerAsync(MessageComponentInteraction interaction, string widgetId)
    {
        // Must be in a guild
        if (!interaction.GuildId.HasValue)
            throw new SlashCommandBusinessException(Strings.Exceptions.DlEmotesNotInGuild);

        var state = LoadEmoteWidgetState(widgetId);
        EnsureInvoker(interaction, state);

        // User must have permission to create expressions
        if (interaction.User is GuildInteractionUser guildUser
            && !guildUser.Permissions.HasFlag(Permissions.CreateGuildExpressions)
            && !guildUser.Permissions.HasFlag(Permissions.ManageGuildExpressions))
        {
            throw new SlashCommandBusinessException(Strings.Exceptions.DlEmotesUserMissingEmojiPerm);
        }

        // Bot must have permission to create expressions
        if (!interaction.AppPermissions.HasFlag(Permissions.CreateGuildExpressions)
            && !interaction.AppPermissions.HasFlag(Permissions.ManageGuildExpressions))
        {
            throw new SlashCommandBusinessException(Strings.Exceptions.DlEmotesBotMissingEmojiPerm);
        }

        var httpClient = MainServiceProvider
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient(Core.HttpClientNames.Emotes);

        var downloaded = await DownloadEmotes.DownloadEmotesAsync(state.Emotes, httpClient);

        var restClient = MainServiceProvider.GetRequiredService<Core.Vega>().Rest;
        var (created, failures) = await DownloadEmotes.AddEmotesToGuildAsync(
            interaction.GuildId.Value, downloaded, restClient);

        var locale = interaction.UserLocale;
        string content = failures.Count == 0
            ? ResourceHelper.GetString(Strings.Commands.DlEmotesAddedAll, locale, created)
            : ResourceHelper.GetString(
                Strings.Commands.DlEmotesAddedPartial,
                locale,
                created,
                failures.Count,
                string.Join("\n", failures.Select(f => "• " + f))
            );

        await interaction.SendFollowupMessageAsync(new InteractionMessageProperties
        {
            Content = content,
            Flags = MessageFlags.Ephemeral
        });
    }

    private static EmoteWidgetState LoadEmoteWidgetState(string widgetId)
    {
        var cache = MainServiceProvider.GetRequiredService<IMemoryCache>();
        if (!cache.TryGetValue(DownloadEmotes.CACHE_KEY_PREFIX + widgetId, out EmoteWidgetState? state) || state == null)
            throw new SlashCommandBusinessException(Strings.Exceptions.DlEmotesWidgetExpired);

        return state;
    }

    private static void EnsureInvoker(MessageComponentInteraction interaction, EmoteWidgetState state)
    {
        if (interaction.User.Id != state.InvokerId)
            throw new SlashCommandBusinessException(Strings.Exceptions.DlEmotesNotInvoker);
    }

    #endregion
}
