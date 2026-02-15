using System.Collections.Concurrent;
using Core;
using Exceptions;
using Microsoft.EntityFrameworkCore;
using Models.Entities;
using NetCord;
using NetCord.Rest;
using static Core.GlobalRegistry;

namespace Services;

public class ReminderService
{
    private readonly AppDbContext _dbContext;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _reminderTimers = new();
    private RestClient _restClient = null!;
    
    public ReminderService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Initializes the service with the RestClient (must be called after Vega.Initialize())
    /// and loads existing reminders from DB
    /// </summary>
    public async Task Initialize(RestClient restClient)
    {
        _restClient = restClient;

        // Load all existing reminders from DB and initiate their timers
        var existingReminders = await _dbContext.Reminders
            .Where(r => !r.IsCompleted && r.RemindAt > DateTime.UtcNow)
            .ToListAsync();

        foreach (var reminder in existingReminders)
        {
            ScheduleReminder(reminder);
        }
    }

    #region Reminder Management methods

    /// <summary>
    /// Creates a new reminder in DB and schedules it
    /// </summary>
    public async Task<Reminder> CreateReminderAsync(Reminder reminder)
    {
        _dbContext.Reminders.Add(reminder);
        await _dbContext.SaveChangesAsync();

        ScheduleReminder(reminder);

        return reminder;
    }

    /// <summary>
    /// Retrieves all active reminders for a user in a guild
    /// </summary>
    public async Task<List<Reminder>> GetUserRemindersAsync(ulong userId, ulong guildId)
    {
        return await _dbContext.Reminders
            .Where(r => r.UserId == userId && r.GuildId == guildId && !r.IsCompleted)
            .OrderBy(r => r.RemindAt)
            .ToListAsync();
    }

    /// <summary>
    /// Deletes a specific reminder by ID
    /// </summary>
    public async Task DeleteReminderAsync(Guid reminderId, ulong userId)
    {
        var reminder = await _dbContext.Reminders
            .FirstOrDefaultAsync(r => r.ReminderId == reminderId && r.UserId == userId);

        if (reminder == null)
        {
            throw new SlashCommandBusinessException("Reminder not found");
        }

        _dbContext.Reminders.Remove(reminder);
        await _dbContext.SaveChangesAsync();

        // Cancel the timer if it exists
        if (_reminderTimers.TryRemove(reminderId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    /// <summary>
    /// Deletes all reminders for a user in a guild
    /// </summary>
    public async Task<int> DeleteAllUserRemindersAsync(ulong userId, ulong guildId)
    {
        var reminders = await _dbContext.Reminders
            .Where(r => r.UserId == userId && r.GuildId == guildId && !r.IsCompleted)
            .ToListAsync();

        foreach (var reminder in reminders)
        {
            if (_reminderTimers.TryRemove(reminder.ReminderId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
        }

        _dbContext.Reminders.RemoveRange(reminders);
        await _dbContext.SaveChangesAsync();

        return reminders.Count;
    }

    /// <summary>
    /// Snoozes a reminder by adding time to it
    /// </summary>
    public async Task SnoozeReminderAsync(Guid reminderId, ulong userId, TimeSpan snoozeTime)
    {
        var reminder = await _dbContext.Reminders
            .FirstOrDefaultAsync(r => r.ReminderId == reminderId && r.UserId == userId && !r.IsCompleted);

        if (reminder == null)
        {
            throw new SlashCommandBusinessException("Reminder not found");
        }

        // Cancel existing timer
        if (_reminderTimers.TryRemove(reminderId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        // Update reminder time
        reminder.RemindAt = reminder.RemindAt.Add(snoozeTime);
        await _dbContext.SaveChangesAsync();

        // Reschedule
        ScheduleReminder(reminder);
    }

    #endregion

    #region Private Timer Methods

    /// <summary>
    /// Schedules a reminder to be triggered at the specified time
    /// </summary>
    private void ScheduleReminder(Reminder reminder)
    {
        var delay = reminder.RemindAt - DateTime.UtcNow;
        
        if (delay.TotalMilliseconds <= 0)
        {
            // If the reminder is in the past, trigger it immediately
            _ = TriggerReminderAsync(reminder);
            return;
        }

        var cts = new CancellationTokenSource();
        _reminderTimers[reminder.ReminderId] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, cts.Token);
                
                if (!cts.Token.IsCancellationRequested)
                {
                    await TriggerReminderAsync(reminder);
                }
            }
            catch (TaskCanceledException)
            {
                // Timer was cancelled, do nothing
            }
            finally
            {
                _reminderTimers.TryRemove(reminder.ReminderId, out _);
                cts.Dispose();
            }
        }, cts.Token);
    }

    /// <summary>
    /// Triggers a reminder by sending a message to the user
    /// </summary>
    private async Task TriggerReminderAsync(Reminder reminder)
    {
        try
        {
            // Get the channel
            var channel = await _restClient.GetChannelAsync(reminder.ChannelId);
            
            if (channel is TextChannel textChannel)
            {
                // Send the reminder message
                var embed = new EmbedProperties
                {
                    Title = "🔔 Reminder",
                    Description = reminder.Message,
                    Color = new Color(0x3498db),
                    Footer = new EmbedFooterProperties
                    {
                        Text = $"Set on {reminder.CreatedAt:yyyy-MM-dd HH:mm} UTC"
                    }
                };

                await textChannel.SendMessageAsync(new MessageProperties
                {
                    Content = $"<@{reminder.UserId}>",
                    Embeds = new[] { embed }
                });
            }

            // Mark reminder as completed
            reminder.IsCompleted = true;
            await _dbContext.SaveChangesAsync();

            // Remove timer reference
            _reminderTimers.TryRemove(reminder.ReminderId, out _);
        }
        catch (Exception ex)
        {
            // Log error but don't throw - we don't want to crash the service
            Console.WriteLine($"Error triggering reminder {reminder.ReminderId}: {ex.Message}");
        }
    }

    #endregion
}
