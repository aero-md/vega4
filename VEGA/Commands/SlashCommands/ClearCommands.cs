using Core;
using Core.CustomCommandAttributes;
using Microsoft.Extensions.DependencyInjection;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using Resources;

namespace SlashCommands;

#if DEBUG

public class ClearCommands :  ApplicationCommandModule<ApplicationCommandContext>
{
    [DefferedResponse]
    [SlashCommand("clearcommands", "Erase all registered commands for this bot")]
    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    public async Task Execute(
        [SlashCommandParameter(
            Name = "global",
            Description = "If true, erase globally registerd command. Default: false (guild only)"
        )] bool global = false
    )
    {
        var vegaInstance = GlobalRegistry.MainServiceProvider.GetRequiredService<Vega>();

        if (global)
        {
            await vegaInstance.ClearAllRegisteredCommandsAsync();
        }
        else
        {
            var guidlId = Context.Interaction.Guild!.Id;
            await vegaInstance.ClearAllRegisteredCommandsAsync(guidlId);
        }

        await Context.Interaction.SendFollowupMessageAsync
        (
            ResourceHelper.GetString(
                global ? Strings.Commands.ClearedCommandsGlobal : Strings.Commands.ClearedCommandsGuild,
                Context.Interaction.UserLocale
            )
        );

        Environment.Exit(0);
    }
}

#endif