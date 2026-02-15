using static Core.GlobalRegistry;
using Exceptions;
using Models.Entities;
using NetCord;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using Microsoft.Extensions.DependencyInjection;
using Services;
using Core.CustomCommandAttributes;
using Resources;

/* 
namespace SlashCommands;

[SlashCommand("reminder", "Manage your reminders")]
public class Reminders : ApplicationCommandModule<ApplicationCommandContext>
{
    const int MESSAGE_LENGTH_MAX = 1000;
    const int MESSAGE_LENGTH_MIN = 1;
    const int HOURS_MAX = 8760; // 1 year
    const int HOURS_MIN = 0;
    const int MINUTES_MAX = 59;
    const int MINUTES_MIN = 1;
    const int DAYS_MAX = 365;
    const int DAYS_MIN = 0;

    private readonly ReminderService _reminderService = MainServiceProvider.GetRequiredService<ReminderService>();

    [DefferedResponse]
    [SubSlashCommand("set", "Set a new reminder")]
    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    public async Task SetReminder(
        [SlashCommandParameter(
            Name = "message",
            Description = "What should I remind you about?",
            MaxLength = MESSAGE_LENGTH_MAX,
            MinLength = MESSAGE_LENGTH_MIN
        )]
        string message,
        [SlashCommandParameter(
            Name = "days",
            Description = "Number of days from now",
            MaxValue = DAYS_MAX,
            MinValue = DAYS_MIN
        )]
        int days = 0,
        [SlashCommandParameter(
            Name = "hours",
            Description = "Number of hours from now",
            MaxValue = HOURS_MAX,
            MinValue = HOURS_MIN
        )]
        int hours = 0,
        [SlashCommandParameter(
            Name = "minutes",
            Description = "Number of minutes from now",
            MaxValue = MINUTES_MAX,
            MinValue = MINUTES_MIN
        )]
        int minutes = 1
    )
    {
        try
        {
            if (days == 0 && hours == 0 && minutes == 0)
            {
                throw new SlashCommandBusinessException(Strings.Commands.ReminderTimeRequired);
            }

            var remindAt = DateTime.UtcNow.AddDays(days).AddHours(hours).AddMinutes(minutes);

            if (remindAt <= DateTime.UtcNow)
            {
                throw new SlashCommandBusinessException(Strings.Commands.ReminderInvalidTime);
            }

            var reminder = new Reminder(
                Context.Interaction.User.Id,
                Context.Interaction.Guild!.Id,
                Context.Interaction.Channel.Id,
                message,
                remindAt
            );

            await _reminderService.CreateReminderAsync(reminder);

            var timeUntil = remindAt - DateTime.UtcNow;
            string timeString = FormatTimeSpan(timeUntil);

            await Context.Interaction.SendFollowupMessageAsync(
                ResourceHelper.GetString(
                    Strings.Commands.ReminderSet,
                    Context.Interaction.UserLocale,
                    timeString
                )
            );
        }
        catch (SlashCommandBusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SlashCommandGenericException(ex.Message);
        }
    }

    [DefferedResponse]
    [SubSlashCommand("list", "List all your active reminders")]
    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    public async Task ListReminders()
    {
        try
        {
            var reminders = await _reminderService.GetUserRemindersAsync(
                Context.Interaction.User.Id,
                Context.Interaction.Guild!.Id
            );

            if (reminders.Count == 0)
            {
                await Context.Interaction.SendFollowupMessageAsync(
                    ResourceHelper.GetString(Strings.Commands.NoActiveReminders, Context.Interaction.UserLocale)
                );
                return;
            }

            var fields = new List<EmbedFieldProperties>();

            foreach (var reminder in reminders.Take(25)) // Discord limit is 25 fields
            {
                var timeUntil = reminder.RemindAt - DateTime.UtcNow;
                string timeString = FormatTimeSpan(timeUntil);

                var field = new EmbedFieldProperties
                {
                    Name = $"⏰ In {timeString}",
                    Value = $"{reminder.Message}\n*ID: {reminder.ReminderId.ToString()[..8]}...*"
                };
                fields.Add(field);
            }

            var embed = new EmbedProperties
            {
                Title = ResourceHelper.GetString(Strings.Commands.YourActiveReminders, Context.Interaction.UserLocale),
                Color = new Color(0x3498db),
                Fields = fields
            };

            await Context.Interaction.SendFollowupMessageAsync(
                new InteractionMessageProperties
                {
                    Embeds = new[] { embed }
                }
            );
        }
        catch (Exception ex)
        {
            throw new SlashCommandGenericException(ex.Message);
        }
    }

    [DefferedResponse]
    [SubSlashCommand("delete", "Delete a specific reminder")]
    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    public async Task DeleteReminder(
        [SlashCommandParameter(
            Name = "reminder_id",
            Description = "The ID of the reminder to delete (from /reminder list)"
        )]
        string reminderId
    )
    {
        try
        {
            if (!Guid.TryParse(reminderId, out Guid parsedId))
            {
                throw new SlashCommandBusinessException(Strings.Exceptions.InvalidParams);
            }

            await _reminderService.DeleteReminderAsync(parsedId, Context.Interaction.User.Id);

            await Context.Interaction.SendFollowupMessageAsync(
                ResourceHelper.GetString(Strings.Commands.ReminderDeleted, Context.Interaction.UserLocale)
            );
        }
        catch (SlashCommandBusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SlashCommandGenericException(ex.Message);
        }
    }

    [DefferedResponse]
    [SubSlashCommand("reset", "Delete all your active reminders")]
    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    public async Task ResetReminders()
    {
        try
        {
            int count = await _reminderService.DeleteAllUserRemindersAsync(
                Context.Interaction.User.Id,
                Context.Interaction.Guild!.Id
            );

            await Context.Interaction.SendFollowupMessageAsync(
                ResourceHelper.GetString(
                    Strings.Commands.RemindersReset,
                    Context.Interaction.UserLocale,
                    count
                )
            );
        }
        catch (Exception ex)
        {
            throw new SlashCommandGenericException(ex.Message);
        }
    }

    [DefferedResponse]
    [SubSlashCommand("snooze", "Snooze a reminder by adding more time")]
    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    public async Task SnoozeReminder(
        [SlashCommandParameter(
            Name = "reminder_id",
            Description = "The ID of the reminder to snooze (from /reminder list)"
        )]
        string reminderId,
        [SlashCommandParameter(
            Name = "minutes",
            Description = "Additional minutes to snooze",
            MaxValue = 1440,
            MinValue = 1
        )]
        int minutes = 10
    )
    {
        try
        {
            if (!Guid.TryParse(reminderId, out Guid parsedId))
            {
                throw new SlashCommandBusinessException(Strings.Exceptions.InvalidParams);
            }

            await _reminderService.SnoozeReminderAsync(
                parsedId,
                Context.Interaction.User.Id,
                TimeSpan.FromMinutes(minutes)
            );

            await Context.Interaction.SendFollowupMessageAsync(
                ResourceHelper.GetString(
                    Strings.Commands.ReminderSnoozed,
                    Context.Interaction.UserLocale,
                    minutes
                )
            );
        }
        catch (SlashCommandBusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SlashCommandGenericException(ex.Message);
        }
    }

    #region Helper Methods

    private string FormatTimeSpan(TimeSpan timeSpan)
    {
        var parts = new List<string>();

        if (timeSpan.Days > 0)
            parts.Add($"{timeSpan.Days} day{(timeSpan.Days > 1 ? "s" : "")}");

        if (timeSpan.Hours > 0)
            parts.Add($"{timeSpan.Hours} hour{(timeSpan.Hours > 1 ? "s" : "")}");

        if (timeSpan.Minutes > 0)
            parts.Add($"{timeSpan.Minutes} minute{(timeSpan.Minutes > 1 ? "s" : "")}");

        return parts.Count > 0 ? string.Join(", ", parts) : "less than a minute";
    }

    #endregion
}
*/