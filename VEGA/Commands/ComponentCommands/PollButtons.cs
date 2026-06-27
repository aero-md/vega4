using Exceptions;
using Microsoft.Extensions.DependencyInjection;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;
using Resources;
using Services;
using static Core.GlobalRegistry;

namespace ComponentCommands;

/// <summary>
/// Routes /poll vote button clicks. customId format: "poll_vote:{pollIdHex}_{optionIndex}".
/// The prefix must NOT contain NetCord's parameter separator (':'), so it stays a single
/// segment ("poll_vote"). pollId+index are packed into one trailing segment (split on '_')
/// to keep NetCord's param binding unambiguous against the ':' separator.
/// </summary>
public class PollButtons : ComponentInteractionModule<ButtonInteractionContext>
{
    public const string CUSTOMID_VOTE_PREFIX = "poll_vote";
    public const char CUSTOMID_OUTER_SEPARATOR = ':';
    public const char CUSTOMID_INNER_SEPARATOR = '_';

    [ComponentInteraction(CUSTOMID_VOTE_PREFIX)]
    public async Task VoteAsync(string segment)
    {
        // Ephemeral so other users never see who voted; that's the "anonymous" half of the contract.
        await Context.Interaction.SendResponseAsync(
            InteractionCallback.DeferredMessage(MessageFlags.Ephemeral)
        );

        // Parse "{pollIdHex}_{optionIndex}".
        var sepIdx = segment.LastIndexOf(CUSTOMID_INNER_SEPARATOR);
        if (sepIdx <= 0 || sepIdx == segment.Length - 1)
            throw new SlashCommandBusinessException(Strings.Exceptions.InvalidParams);

        var pollIdStr = segment[..sepIdx];
        var indexStr = segment[(sepIdx + 1)..];

        if (!Guid.TryParseExact(pollIdStr, "N", out var pollId)
            || !int.TryParse(indexStr, out var optionIndex))
        {
            throw new SlashCommandBusinessException(Strings.Exceptions.InvalidParams);
        }

        var pollService = MainServiceProvider.GetRequiredService<PollService>();
        var poll = await pollService.GetPollAsync(pollId);
        var locale = Context.Interaction.UserLocale ?? "en-US";

        if (poll == null || poll.IsCompleted)
            throw new SlashCommandBusinessException(Strings.Exceptions.PollNotFoundOrEnded);

        if (optionIndex < 0 || optionIndex >= poll.Options.Length)
            throw new SlashCommandBusinessException(Strings.Exceptions.InvalidParams);

        bool registered = await pollService.RegisterVoteAsync(
            pollId,
            Context.Interaction.User.Id,
            optionIndex
        );

        string content = registered
            ? ResourceHelper.GetString(Strings.Commands.PollVoteRegistered, locale, poll.Options[optionIndex])
            : ResourceHelper.GetString(Strings.Commands.PollVoteAlreadyVoted, locale);

        await Context.Interaction.SendFollowupMessageAsync(new InteractionMessageProperties
        {
            Content = content,
            Flags = MessageFlags.Ephemeral
        });
    }
}
