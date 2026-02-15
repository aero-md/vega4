using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Core;
using Microsoft.Extensions.DependencyInjection;
using Core.CustomCommandAttributes;

namespace SlashCommands;

public class Up : ApplicationCommandModule<ApplicationCommandContext>
{
    const string SIZE_URL_PARAM = "?size=512";

    [SlashCommand("up", "Indicates uptime and other infos about the bot")]
    public async Task Execute()
    {
        var self = await Context.Client.Rest.GetUserAsync(Context.Client.Id);

        ImageUrl? avatarUrl = self.GetAvatarUrl();
        ImageUrl? bannerUrl = self.GetBannerUrl();

        var uptime = DateTime.UtcNow - GlobalRegistry.StartTime;
        var embed = new EmbedProperties
        {
            Title = "ᴠ.ᴇ.ɢ.ᴀ.",
            Url = "https://github.com/a-e-r-o/vega4",
            Color = new Color(96, 240, 213),
            Fields = new[]
            {
                new EmbedFieldProperties
                {
                    Name = "Uptime",
                    Value = string.Format("{0} days, {1}h, {2}m, {3}s", uptime.Days, uptime.Hours, uptime.Minutes, uptime.Seconds)
                },
                new EmbedFieldProperties
                {
                    Name = "Started at",
                    Value = string.Format
                            (
                                "{0} UTC",
                                GlobalRegistry.StartTime.ToString("yyyy-MM-dd HH:mm:ss")
                            )
                }
            },
            Footer = new EmbedFooterProperties
            {
                Text = "How much further could we march, if we were not forced to carry our fears on our backs ?"
            }
        };

        if(bannerUrl is not null)
            embed.Image = $"{bannerUrl}{SIZE_URL_PARAM}";

        if(avatarUrl is not null)
            embed.Thumbnail = new EmbedThumbnailProperties($"{avatarUrl}{SIZE_URL_PARAM}");

        await Context.Interaction.SendResponseAsync(
            InteractionCallback.Message(
                new InteractionMessageProperties
                {
                    Embeds = new[] { embed }
                }
            )
        );
    }
}