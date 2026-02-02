using Exceptions;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Resources;

namespace SlashCommands;

public class DiceRoll : ApplicationCommandModule<ApplicationCommandContext>
{
    const int FACES_MIN = 2;
    const int FACES_MAX = 100;
    const int FACES_DEFAULT = 6;

    const int ROLLS_MIN = 1;
    const int ROLLS_MAX = 100;
    const int ROLLS_DEFAULT = 1;


    [SlashCommand("diceroll", "Roll a dice")]
    public async Task Execute(
        [SlashCommandParameter(
            Name = "faces", Description = "Number of sides on the dice", MinValue = FACES_MIN, MaxValue = FACES_MAX
        )] int diceFaces = FACES_DEFAULT,
        [SlashCommandParameter(
            Name = "rolls", Description = "Number of dices to roll", MinValue = ROLLS_MIN, MaxValue = ROLLS_MAX
        )] int rollCount = ROLLS_DEFAULT
    )
    {
        // Don't trust Discord on minmax values validation
        if (
            diceFaces > FACES_MAX || 
            diceFaces < FACES_MIN ||
            rollCount > ROLLS_MAX ||
            rollCount < ROLLS_MIN
        ) throw new SlashCommandBusinessException(Strings.Exceptions.InvalidParams);

        List<string> results = new();

        for (int i = 0; i < rollCount; i++)
        {
            int randInt = Random.Shared.Next(1, diceFaces + 1);
            results.Add($"[{randInt}]");
        }

        await Context.Interaction.SendResponseAsync(
            InteractionCallback.Message(
                string.Join("  ", results)
            )
        );
    }
}