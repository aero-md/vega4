using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Models.Entities;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;
using Resources;
using Services;
using static Core.GlobalRegistry;

namespace ComponentCommands;

/// <summary>
/// Submit handler for the /poll creation modal. Replaces the old inline slash args:
/// question, duration, options (one per line — empty = Yes/No), optional image URL.
/// Validates, persists the poll, then posts the public embed + vote buttons.
/// </summary>
public class PollModal : ComponentInteractionModule<ModalInteractionContext>
{
    public const string CUSTOMID = "poll_create";
    public const string FIELD_QUESTION = "poll_question";
    public const string FIELD_DURATION = "poll_duration";
    public const string FIELD_OPTIONS = "poll_options";
    public const string FIELD_IMAGE = "poll_image";

    private const int QUESTION_MAX = 500;
    private const int OPTION_LENGTH_MAX = 80;
    private const int MAX_OPTIONS = 5;          // one Discord action row holds at most 5 buttons
    private const double DURATION_HOURS_MIN = 0.01;
    private const double DURATION_HOURS_MAX = 168; // 1 week

    [ComponentInteraction(CUSTOMID)]
    public async Task SubmitAsync()
    {
        var locale = Context.Interaction.UserLocale ?? "en-US";

        var question = (GetInput(FIELD_QUESTION) ?? "").Trim();
        var durationRaw = (GetInput(FIELD_DURATION) ?? "").Trim().Replace(',', '.');
        var optionsRaw = GetInput(FIELD_OPTIONS) ?? "";
        var image = (GetInput(FIELD_IMAGE) ?? "").Trim();

        if (question.Length is < 1 or > QUESTION_MAX)
        {
            await ErrorAsync(locale);
            return;
        }

        if (!double.TryParse(durationRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var durationHours)
            || durationHours is < DURATION_HOURS_MIN or > DURATION_HOURS_MAX)
        {
            await ErrorAsync(locale);
            return;
        }

        if (!string.IsNullOrWhiteSpace(image)
            && (!Uri.TryCreate(image, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
        {
            await ErrorAsync(locale);
            return;
        }

        // One option per line; blanks dropped. Empty → Yes/No fallback (localized at creation).
        var providedOptions = optionsRaw
            .Split('\n')
            .Select(o => o.Trim())
            .Where(o => !string.IsNullOrEmpty(o))
            .ToArray();

        if (providedOptions.Length > MAX_OPTIONS || providedOptions.Any(o => o.Length > OPTION_LENGTH_MAX))
        {
            await ErrorAsync(locale);
            return;
        }

        string[] effectiveOptions = providedOptions.Length == 0
            ? new[]
            {
                ResourceHelper.GetString(Strings.Commands.PollOptionYes, locale),
                ResourceHelper.GetString(Strings.Commands.PollOptionNo, locale)
            }
            : providedOptions;

        var endAt = DateTime.UtcNow.AddHours(durationHours);
        var pollService = MainServiceProvider.GetRequiredService<PollService>();

        var poll = new Poll(
            Context.Interaction.GuildId ?? 0,
            Context.Channel.Id,
            Context.Interaction.User.Id,
            question,
            string.IsNullOrWhiteSpace(image) ? null : image,
            effectiveOptions,
            endAt,
            locale
        );

        // Public response: defer (so we can post the poll publicly and get its message id).
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage());

        await pollService.CreatePollAsync(poll);

        var endsAt = ResourceHelper.GetString(
            Strings.Commands.PollEndsAt, locale, ((DateTimeOffset)endAt).ToUnixTimeSeconds());

        var embed = new EmbedProperties
        {
            Title = ResourceHelper.GetString(Strings.Commands.PollTitle, locale),
            Description = $"{question}\n\n{endsAt}",
            Color = new Color(0x9b59b6)
        };
        if (!string.IsNullOrWhiteSpace(image))
            embed.Image = new EmbedImageProperties(image);

        var pollIdStr = poll.PollId.ToString("N");
        var rowButtons = new IActionRowComponentProperties[effectiveOptions.Length];
        for (int i = 0; i < effectiveOptions.Length; i++)
        {
            rowButtons[i] = new ButtonProperties(
                $"{PollButtons.CUSTOMID_VOTE_PREFIX}{PollButtons.CUSTOMID_OUTER_SEPARATOR}{pollIdStr}{PollButtons.CUSTOMID_INNER_SEPARATOR}{i}",
                effectiveOptions[i],
                ButtonStyle.Primary);
        }

        var message = await Context.Interaction.SendFollowupMessageAsync(new InteractionMessageProperties
        {
            Embeds = new[] { embed },
            Components = new IMessageComponentProperties[] { new ActionRowProperties(rowButtons) }
        });

        await pollService.SetMessageIdAsync(poll.PollId, message.Id);
    }

    private async Task ErrorAsync(string locale) =>
        await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties
        {
            Content = ResourceHelper.GetString(Strings.Exceptions.InvalidParams, locale),
            Flags = MessageFlags.Ephemeral
        }));

    private string? GetInput(string customId) =>
        Context.Components
            .OfType<Label>()
            .Select(l => l.Component)
            .OfType<TextInput>()
            .FirstOrDefault(ti => ti.CustomId == customId)?.Value;
}
