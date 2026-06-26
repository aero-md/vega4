using ComponentCommands;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using Resources;

namespace SlashCommands;

public class PollCommand : ApplicationCommandModule<ApplicationCommandContext>
{
    // No [DefferedResponse]: a modal must be the immediate interaction response.
    // The form fields are read and validated on submit by PollModal.
    [SlashCommand("poll", "Start an anonymous poll — opens a form to fill in.")]
    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    public async Task Open()
    {
        var locale = Context.Interaction.UserLocale ?? "en-US";

        var modal = new ModalProperties(
            PollModal.CUSTOMID,
            ResourceHelper.GetString(Strings.Commands.PollModalTitle, locale),
            new IModalComponentProperties[]
            {
                new LabelProperties(
                    ResourceHelper.GetString(Strings.Commands.PollFieldQuestion, locale),
                    new TextInputProperties(PollModal.FIELD_QUESTION, TextInputStyle.Paragraph)
                    {
                        Required = true, MinLength = 1, MaxLength = 500
                    }),
                new LabelProperties(
                    ResourceHelper.GetString(Strings.Commands.PollFieldDuration, locale),
                    new TextInputProperties(PollModal.FIELD_DURATION, TextInputStyle.Short)
                    {
                        Required = true, MaxLength = 8, Placeholder = "24"
                    }),
                new LabelProperties(
                    ResourceHelper.GetString(Strings.Commands.PollFieldOptions, locale),
                    new TextInputProperties(PollModal.FIELD_OPTIONS, TextInputStyle.Paragraph)
                    {
                        Required = false
                    }),
                new LabelProperties(
                    ResourceHelper.GetString(Strings.Commands.PollFieldImage, locale),
                    new TextInputProperties(PollModal.FIELD_IMAGE, TextInputStyle.Short)
                    {
                        Required = false, MaxLength = 500
                    })
            });

        await Context.Interaction.SendResponseAsync(InteractionCallback.Modal(modal));
    }
}
