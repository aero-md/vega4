using Core.CustomCommandAttributes;
using Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Models.Entities;
using NetCord;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using Services;
using Resources;
using static Core.GlobalRegistry;
using NetCord.Services.Commands;

namespace SlashCommands;

[SlashCommand("trigger", "Manage triggers patterns for this server")]
public class Triggers : ApplicationCommandModule<ApplicationCommandContext>
{
    const int REGEX_MIN_LENGTH = 3;
    const int REGEX_MAX_LENGTH = 50;
    const int RESPONSE_MIN_LENGTH = 1;
    const int RESPONSE_MAX_LENGTH = 2000;
    

    [DefferedResponse]
    [SubSlashCommand("list", "List triggers on this server")]
    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    public async Task List()
    {
        GuildSettingsService service = MainServiceProvider.GetRequiredService<GuildSettingsService>();
        GuildSettings settings = await service.GetByIdAsync(Context.Interaction.Guild!.Id);

        var triggers = settings.Triggers.OrderByDescending(x => x.CreatedAt)
                                        .ToList();

        List<string> resMessages = [];

        if (triggers.Count == 0)
        {
            resMessages.Add(ResourceHelper.GetString(Strings.Commands.NoTriggersOnServer, Context.Interaction.UserLocale));
        }
        else
        {
            resMessages.Add(ResourceHelper.GetString(Strings.Commands.CurrentTriggersHeader, Context.Interaction.UserLocale));

            for (int i = 0; i < triggers.Count; i++)
            {
                Trigger iTgr = triggers[i];
                string currentTriggerInfo = ResourceHelper.GetString(
                    Strings.Commands.TriggerInfo, Context.Interaction.UserLocale, i, iTgr.Pattern, iTgr.Response, iTgr.RegexOptions, iTgr.PingOnReply
                );

                // Discord message char limit is 2000
                if (resMessages.Last().Length + currentTriggerInfo.Length > 2000)
                {
                    // If current message would exeed limit with this trigger, add it to the next message instead
                    resMessages.Add(string.Empty);
                } 
                resMessages[^1] += currentTriggerInfo;
            }
        }

        // Send first part of response
        await Context.Interaction.SendFollowupMessageAsync(
            resMessages[0]
        );

        // Send eventual additionnal messages if limited by limit of 2000 chars
        if (resMessages.Count > 1)
        {
            for (int i = 1; i < resMessages.Count; i++)
            {
                await Context.Channel.SendMessageAsync(resMessages[i]);
            }
        }
    }

    [DefferedResponse]
    [SubSlashCommand("add", "Add a new trigger")]
    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [RequireUserPermissions<ApplicationCommandContext>(Permissions.ManageMessages)]
    public async Task Add(
        [SlashCommandParameter(
            Name = "regex",
            Description = "Pattern to match using regex notation",
            MinLength = REGEX_MIN_LENGTH, MaxLength = REGEX_MAX_LENGTH
        )] string regex,
        [SlashCommandParameter(
            Name = "response",
            Description = "Response message to send when pattern is detected",
            MinLength = RESPONSE_MIN_LENGTH, MaxLength = RESPONSE_MAX_LENGTH
        )] string response,
        [SlashCommandParameter(
            Name = "regexoptions",
            Description = "Regex matching options flag : see .NET Regular expression options",
            MaxLength = 10
        )] int regexOptions = 0
    )
    {
        // Don't trust Discord on minmax values validation
        if (
            regex.Length > REGEX_MAX_LENGTH || regex.Length < REGEX_MIN_LENGTH ||
            response.Length > RESPONSE_MAX_LENGTH || response.Length < RESPONSE_MIN_LENGTH
        ) throw new SlashCommandBusinessException(Strings.Exceptions.InvalidParams);

        GuildSettingsService service = MainServiceProvider.GetRequiredService<GuildSettingsService>();
        var guildId = 
            Context.Interaction.GuildId ?? throw new SlashCommandBusinessException(Strings.Exceptions.UnableToRetrieveGuild);

        // Create and add new trigger
        Trigger newTrigger = new Trigger(guildId, regex, response, regexOptions);        
        _ = await service.AddTrigger(guildId, newTrigger);

        await Context.Interaction.SendFollowupMessageAsync(
            ResourceHelper.GetString(Strings.Commands.TriggerAdded, Context.Interaction.UserLocale, regex, regexOptions)
        );
    }

    [DefferedResponse]
    [SubSlashCommand("delete", "Delete a trigger by ID (see ID in trigger list)")]
    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [RequireUserPermissions<ApplicationCommandContext>(Permissions.ManageMessages)]
    public async Task Delete(
        [SlashCommandParameter(
            Name = "id",
            Description = "ID of the trigger to delete"
        )] int triggerIndex
    )
    {
        GuildSettingsService service = MainServiceProvider.GetRequiredService<GuildSettingsService>();
        ulong guildId = Context.Interaction.GuildId ?? throw new SlashCommandBusinessException(Strings.Exceptions.UnableToRetrieveGuild);

        string? deletedPattern = await service.DeleteTrigger(guildId, triggerIndex) ?? throw new SlashCommandBusinessException(Strings.Exceptions.TriggerNotFound);
        
        await Context.Interaction.SendFollowupMessageAsync(
            ResourceHelper.GetString(Strings.Commands.TriggerDeleted, Context.Interaction.UserLocale, deletedPattern)
        );
    }
}