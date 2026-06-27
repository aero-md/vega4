using System.Collections.Concurrent;
using Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Models.Entities;
using NetCord;
using NetCord.Rest;
using Resources;

namespace Services;

/// <summary>
/// Owns the lifecycle of polls: persists them, schedules an end timer, accepts votes,
/// and posts the result embed when the timer fires (then deletes the original message).
/// Mirrors FeedService's IServiceScopeFactory pattern so the singleton can safely use a scoped DbContext.
/// </summary>
public class PollService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PollService> _logger;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _timers = new();
    private RestClient _restClient = null!;

    public PollService(IServiceScopeFactory scopeFactory, ILogger<PollService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Initializes the service with the RestClient (must be called after Vega.Initialize())
    /// and reschedules timers for any poll that was still pending when the bot last shut down.
    /// </summary>
    public async Task Initialize(RestClient restClient)
    {
        _restClient = restClient;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pending = await db.Polls
            .Where(p => !p.IsCompleted)
            .ToListAsync();

        foreach (var poll in pending)
        {
            ScheduleEnd(poll);
        }

        _logger.LogInformation("PollService initialized with {Count} pending poll(s)", pending.Count);
    }

    /// <summary>
    /// Persists the poll and schedules its end timer. The MessageId is set later by SetMessageIdAsync,
    /// once the embed message has been posted.
    /// </summary>
    public async Task CreatePollAsync(Poll poll)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Polls.Add(poll);
        await db.SaveChangesAsync();

        ScheduleEnd(poll);
    }

    public async Task SetMessageIdAsync(Guid pollId, ulong messageId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var poll = await db.Polls.FirstOrDefaultAsync(p => p.PollId == pollId);
        if (poll == null) return;

        poll.MessageId = messageId;
        await db.SaveChangesAsync();
    }

    public async Task<Poll?> GetPollAsync(Guid pollId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Polls.FirstOrDefaultAsync(p => p.PollId == pollId);
    }

    /// <summary>
    /// Inserts a vote. Returns false if the user already voted on this poll.
    /// PK constraint on (PollId, UserId) is the source of truth — the explicit AnyAsync
    /// avoids most exceptions but DbUpdateException is still caught for the rare race.
    /// </summary>
    public async Task<bool> RegisterVoteAsync(Guid pollId, ulong userId, int optionIndex)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var alreadyVoted = await db.PollVotes
            .AnyAsync(v => v.PollId == pollId && v.UserId == userId);
        if (alreadyVoted) return false;

        try
        {
            db.PollVotes.Add(new PollVote(pollId, userId, optionIndex));
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    private void ScheduleEnd(Poll poll)
    {
        var cts = new CancellationTokenSource();
        _timers.AddOrUpdate(
            poll.PollId,
            cts,
            (_, old) => { old.Dispose(); return cts; }
        );

        _ = Task.Run(async () =>
        {
            try
            {
                var delay = poll.EndAt - DateTime.UtcNow;
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, cts.Token);

                if (!cts.Token.IsCancellationRequested)
                    await EndPollAsync(poll.PollId);
            }
            catch (TaskCanceledException) { /* timer cancelled */ }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in poll timer for {PollId}", poll.PollId);
            }
            finally
            {
                _timers.TryRemove(poll.PollId, out _);
                cts.Dispose();
            }
        });
    }

    /// <summary>
    /// Tally votes, post the result embed, delete the original poll message,
    /// and mark the poll as completed.
    /// </summary>
    private async Task EndPollAsync(Guid pollId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var poll = await db.Polls.FirstOrDefaultAsync(p => p.PollId == pollId);
        if (poll == null || poll.IsCompleted) return;

        var votes = await db.PollVotes
            .Where(v => v.PollId == pollId)
            .ToListAsync();

        var tally = new int[poll.Options.Length];
        foreach (var v in votes)
        {
            if (v.OptionIndex >= 0 && v.OptionIndex < tally.Length)
                tally[v.OptionIndex]++;
        }

        var locale = poll.Locale;
        int total = votes.Count;

        var fields = new List<EmbedFieldProperties>();
        for (int i = 0; i < poll.Options.Length; i++)
        {
            int count = tally[i];
            double pct = total > 0 ? 100.0 * count / total : 0;
            fields.Add(new EmbedFieldProperties
            {
                Name = poll.Options[i],
                Value = ResourceHelper.GetString(Strings.Commands.PollResultsField, locale, count, pct.ToString("0.0"))
            });
        }

        var resultEmbed = new EmbedProperties
        {
            Title = ResourceHelper.GetString(Strings.Commands.PollResultsTitle, locale),
            Description = poll.Question,
            Color = new Color(0x9b59b6),
            Fields = fields,
            Footer = new EmbedFooterProperties
            {
                Text = ResourceHelper.GetString(Strings.Commands.PollResultsFooter, locale, total)
            }
        };

        try
        {
            var channel = await _restClient.GetChannelAsync(poll.ChannelId);
            if (channel is TextChannel textChannel)
            {
                await textChannel.SendMessageAsync(new MessageProperties
                {
                    Embeds = new[] { resultEmbed }
                });
            }
            else
            {
                _logger.LogWarning("Poll {PollId} channel {ChannelId} is not a text channel; skipping result post",
                    pollId, poll.ChannelId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post poll results for {PollId}", pollId);
        }

        // Best-effort delete of the original embed (it's the bot's own message, no perm required).
        if (poll.MessageId != 0)
        {
            try
            {
                await _restClient.DeleteMessageAsync(poll.ChannelId, poll.MessageId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete original poll message {MessageId}", poll.MessageId);
            }
        }

        poll.IsCompleted = true;
        await db.SaveChangesAsync();
    }
}
