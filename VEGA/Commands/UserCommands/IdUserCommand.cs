using NetCord;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using Resources;

namespace UserCommands;

public class IdModule : ApplicationCommandModule<ApplicationCommandContext>
{
    [UserCommand("ID")]
    public async Task Execute(User user){
        await Context.Interaction.SendResponseAsync(
            InteractionCallback.Message(
                new InteractionMessageProperties
                {
                    Content = $"`{user.Id}`",
                    Flags = MessageFlags.Ephemeral
                }
            )
        );
    }
}