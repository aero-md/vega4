using static Core.GlobalRegistry;
using Core;
using Microsoft.Extensions.DependencyInjection;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using Models.Entities;
using System.Text.RegularExpressions;
using Services;
using Serilog;

namespace Handlers;

public static class MessageCreateHandler
{
    public static async Task MessageCreate(GatewayClient client, Message message)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Check if message channel exists and is in guild and is not from a bot
        if (message.GuildId.HasValue && message.Channel is not null && !message.Author.IsBot)
        {
            GuildSettingsService service = MainServiceProvider.GetRequiredService<GuildSettingsService>();
            GuildSettings settings = await service.GetByIdAsync(message.GuildId.Value);

            // No triggers set for this guild
            if (settings.Triggers.Count != 0)
            {
                Trigger? trigger = checkTriggers(message, settings);

                if (trigger != null)
                {
                    try
                    {
                        if (trigger.PingOnReply)
                        {
                            await message.Channel.SendMessageAsync(new MessageProperties
                            {
                                Content = trigger.Response,
                                AllowedMentions = AllowedMentionsProperties.None
                            });
                        }
                        else
                        {
                            await message.ReplyAsync(new ReplyMessageProperties
                            {
                                Content = trigger.Response,
                                AllowedMentions = AllowedMentionsProperties.None
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to send response to trigger {0} in server {1}", trigger?.TriggerId, message.GuildId);
                    }
                }
            }
        }

        stopwatch.Stop();
    }

    private static Trigger? checkTriggers(Message msg, GuildSettings settings){
        // Find if the message matches any trigger pattern
        for (var i = 0; i < settings.Triggers.Count; i++) {
            var pattern = settings.Triggers[i];
            try {
                // Sanitize stored options at evaluation time too: triggers persisted
                // before the whitelist was introduced may carry forbidden flags.
                var options = TriggerRegex.Sanitize(pattern.RegexOptions);
                if (Regex.IsMatch(msg.Content, pattern.Pattern, options, TriggerRegex.Timeout))
                {
                    return pattern;
                }
            }
            catch (RegexMatchTimeoutException ex)
            {
                Log.Warning(ex, "Regex timeout on trigger {TriggerId} in guild {GuildId} (pattern length {PatternLength})",
                    pattern.TriggerId, pattern.GuildId, pattern.Pattern.Length);
            }
            catch (ArgumentException ex)
            {
                Log.Warning(ex, "Invalid regex pattern on trigger {TriggerId} in guild {GuildId}",
                    pattern.TriggerId, pattern.GuildId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error evaluating trigger {TriggerId} in guild {GuildId}",
                    pattern.TriggerId, pattern.GuildId);
            }
        }

        return null;
    }
}