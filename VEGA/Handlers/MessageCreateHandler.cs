using static Core.GlobalRegistry;
using Microsoft.Extensions.DependencyInjection;
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
                            await message.Channel.SendMessageAsync(trigger.Response);
                        else
                            await message.ReplyAsync(trigger.Response);
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
        Trigger foundPattern;

        // Find if the message matches any trigger pattern
        for (var i = 0; i < settings.Triggers.Count; i++) {
            var pattern = settings.Triggers[i];
            try {
                if (Regex.IsMatch(msg.Content, pattern.Pattern, (RegexOptions)pattern.RegexOptions))
                {
                    foundPattern = pattern;
                    return foundPattern;
                }
            } 
            catch (Exception)
            {
                
            }
        }

        return null;
    }
}