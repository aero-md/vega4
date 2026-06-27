using static Core.GlobalRegistry;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord;
using NetCord.Services;
using Exceptions;
using NetCord.Services.ApplicationCommands;
using Microsoft.Extensions.DependencyInjection;
using Core.CustomCommandAttributes;
using Models.Core;
using Resources;
using Serilog;

namespace Handlers;

public static class CommandInteractionHandler
{
    public static async Task HandleCommand (
        GatewayClient client,
        ApplicationCommandService<ApplicationCommandContext> appCommandService,
        ApplicationCommandInteraction interaction
    )
    {
        // Diagnostic stopwatch
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Retrieve config
        var config = MainServiceProvider.GetRequiredService<VegaConfiguration>();

        // Setup vars
        string? errorMsg = null;
        bool hasDefferedResponse = false;
        bool isDefferedResponseEphemeral = false;
        bool isSuperAdminRequired  = false;

        // Find command info
        ApplicationCommandInfo<ApplicationCommandContext>? command = appCommandService.GetCommands().SingleOrDefault(c => c.Name == interaction.Data.Name);

        // Variable to hold attributes from either command or subcommand
        IReadOnlyDictionary<Type, IReadOnlyList<Attribute>>? commandAttributes = null;

        switch (command) 
        {
            // Sub slash command in a slash command group
            case SlashCommandGroupInfo<ApplicationCommandContext> slashCommandGroupInfo: 
                // Consider interaction as SlashCommandInteraction to access options
                SlashCommandInteraction? slashinteraction = interaction as SlashCommandInteraction;
                // Find subcommand info
                ISubSlashCommandInfo<ApplicationCommandContext>? subCommand = slashCommandGroupInfo.SubCommands.SingleOrDefault(x => x.Value.Name == slashinteraction?.Data.Options[0].Name).Value;
                // Get attributes from subcommand
                commandAttributes = subCommand?.Attributes;
                break;
            
            // Regular command (not a subcommand)
            default:
                commandAttributes = command?.Attributes;
                break;
            
            // Possible other cases in the future : message command, user command
        }

        
        // Deffered response attribute check + ephemeral option
        Attribute? defferedResponseAttr = null;
        defferedResponseAttr = commandAttributes?.GetValueOrDefault(typeof(DefferedResponseAttribute))?[0];
        hasDefferedResponse = defferedResponseAttr != null;
        isDefferedResponseEphemeral = (defferedResponseAttr as DefferedResponseAttribute)?.Ephemeral ?? false;
        
        // Super admin requirement check
        isSuperAdminRequired = commandAttributes?.ContainsKey(typeof(RequireSuperAdminAttribute)) ?? false;

        try
        {
            // Check for super admin requirement
            if(isSuperAdminRequired && !config.SuperAdminUserIds.Contains(interaction.User.Id))
                throw new RequireSuperAdminException();
            
            // Defer response if required by attribute
            if(hasDefferedResponse)
            {
                // Ephemeral option
                if(isDefferedResponseEphemeral)
                {
                    await interaction.SendResponseAsync(
                         InteractionCallback.DeferredMessage(MessageFlags.Ephemeral)
                    );
                }
                // Non-ephemeral deffered response
                else
                {
                    await interaction.SendResponseAsync(
                        InteractionCallback.DeferredMessage()
                    );
                }
            }

            // Command execution
            IExecutionResult result = await appCommandService.ExecuteAsync(
                new ApplicationCommandContext(interaction, client), serviceProvider: MainServiceProvider
            ).ConfigureAwait(false);

            // Case of exception thrown : rethrow the exception wrapped in the execution result
            if (result is ExecutionExceptionResult executionExceptionResult)
                throw executionExceptionResult.Exception; 

            // Case of missing perm : wrap MissingPerm object into a custom exception and throw it
            if (result is MissingPermissionsResult missingPerm)
            {
                throw new MissingPermissionException(missingPerm);
            }
        }
        catch (MissingPermissionException pex)
        {   
            var missingPerms = Enum.GetValues<Permissions>()
                                    .Cast<Permissions>()
                                    .Where(flag => pex.MissingPerm.MissingPermissions.HasFlag(flag))
                                    .Select(flag => flag.ToString());

            string strMissingPerms = string.Join(",", missingPerms);

            switch (pex.MissingPerm.EntityType) 
            {
                case MissingPermissionsResultEntityType.Bot: 
                    errorMsg = ResourceHelper.GetString(Strings.Exceptions.BotMissingPermissions, interaction.UserLocale, strMissingPerms);
                    break;
                case MissingPermissionsResultEntityType.User:
                    errorMsg = ResourceHelper.GetString(Strings.Exceptions.UserMissingPermissions, interaction.UserLocale, strMissingPerms);
                    break;
            }
        }
        // Expected exception with user-readable message
        catch (SlashCommandBusinessException bex)
        {
            errorMsg = ResourceHelper.GetString(bex.Message, interaction.UserLocale, bex.Args ?? Array.Empty<object>());
        }
        // Super admin requirement not met. Exception thrown before deffered response, so no deffered response exists
        catch(RequireSuperAdminException)
        {
            errorMsg = ResourceHelper.GetString(Strings.Exceptions.RequireSuperAdmin, interaction.UserLocale);
            hasDefferedResponse = false;
        }
        // Unexpected, caught exception
        catch (SlashCommandGenericException)
        {
            errorMsg = ResourceHelper.GetString(Strings.Exceptions.CommandExecutionFailed, interaction.UserLocale);
        }
        // Worst case scenario : unexcepted uncaught exception
        catch (Exception)
        {
            errorMsg = ResourceHelper.GetString(Strings.Exceptions.CommandExecutionCritical, interaction.UserLocale);
        }

        // If any exception occurred, send failure response
        if (errorMsg != null)
        {
            try
            {
                // Reply in followup response to the deferred message
                if (hasDefferedResponse)
                {
                    await interaction.SendFollowupMessageAsync(
                        errorMsg
                    );
                }
                // Reply to interaction
                else
                {   
                    var elapsed = DateTimeOffset.UtcNow - interaction.CreatedAt;

                    // Check if interaction can still be responded to directly
                    // If not, log and do nothing
                    if (elapsed.TotalSeconds > 2.5)
                    {
                        Log.Warning("Interaction response timeout for command {CommandName}", interaction.Data.Name);
                    }
                    else
                    {
                        await interaction.SendResponseAsync(
                            InteractionCallback.Message(
                                new InteractionMessageProperties
                                {
                                    Content = errorMsg,
                                    Flags = MessageFlags.Ephemeral
                                }
                            )
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to send interaction error response for command {CommandName}", interaction.Data.Name);
            }
        }

        stopwatch.Stop();
    }
}