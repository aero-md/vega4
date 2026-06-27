using Core.CustomCommandAttributes;
using Exceptions;
using NetCord;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using Resources;

namespace SlashCommands;

public class ClearMsgs :  ApplicationCommandModule<ApplicationCommandContext>
{
    const int MSG_COUNT_MIN = 1;
    const int MSG_COUNT_MAX = 100;
    
    [DefferedResponse(ephemeral: true)]
    [SlashCommand("clear", "Deletes recent messages")]
    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [RequireUserPermissions<ApplicationCommandContext>(Permissions.ManageMessages)]
    [RequireBotPermissions<ApplicationCommandContext>(Permissions.ManageMessages)]
    public async Task Execute(
        [SlashCommandParameter(
            Name = "count",
            Description = "Number of messages to delete",
            MinValue = MSG_COUNT_MIN,
            MaxValue = MSG_COUNT_MAX
        )]
        int msgCount
    )
    {
        // Don't trust Discord on minmax values validation
        if (
            msgCount > MSG_COUNT_MAX || msgCount < MSG_COUNT_MIN
        ) throw new SlashCommandBusinessException(Strings.Exceptions.InvalidParams);

        try
        {
            IAsyncEnumerable<RestMessage> messages = Context.Channel.GetMessagesAsync(
                new PaginationProperties<ulong> 
                {
                    BatchSize = 100
                }
            );

            List<ulong> msgIds = new();

            await foreach (var message in messages)
            {
                msgIds.Add(message.Id);
                
                if (msgIds.Count >= msgCount)
                {
                    break;
                }
            }

            await Context.Channel.DeleteMessagesAsync(msgIds);

            await Context.Interaction.SendFollowupMessageAsync(
                new InteractionMessageProperties
                {
                    Content = ResourceHelper.GetString(Strings.Commands.DeletedMessages, Context.Interaction.UserLocale, msgIds.Count),
                    Flags = MessageFlags.Ephemeral
                }
            );
        }
        catch(Exception ex)
        {
            throw new SlashCommandGenericException(ex.Message);
        }
    }
}