using Core.CustomCommandAttributes;
using Exceptions;
using Microsoft.AspNetCore.Mvc.Routing;
using NetCord;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using Resources;

namespace SlashCommands;

public class ShowProfile :  ApplicationCommandModule<ApplicationCommandContext>
{
    const string SIZE_URL_PARAM = "?size=512";

    [DefferedResponse]
    [SlashCommand("showprofile", "Show avatar and banner of a user in high res")]
    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    public async Task Execute(
        [SlashCommandParameter(
            Name = "userid",
            Description = "Discord user ID"
        )]
        string strUserId
    )
    {
        // Sanitize input
        strUserId = new string(strUserId.Where(char.IsDigit).ToArray());

        // Validate user ID
        if (!ulong.TryParse(strUserId, out ulong userId)) throw new SlashCommandBusinessException(Strings.Exceptions.IncorrectId);

        try
        {
            User user = await Context.Client.Rest.GetUserAsync(userId) ?? throw new SlashCommandBusinessException(Strings.Exceptions.UserNotFound);

            ImageUrl? avatarUrl = user.GetAvatarUrl();
            ImageUrl? bannerUrl = user.GetBannerUrl();

            var embed = new EmbedProperties
            {
                Title = user.GlobalName,
                Color = user.AccentColor ?? new Color(26, 28, 36),
                Fields = new[]
                {
                    new EmbedFieldProperties
                    {
                        Name = ResourceHelper.GetString(Strings.Commands.Username, Context.Interaction.UserLocale),
                        Value = user.Username
                    },
                    new EmbedFieldProperties
                    {
                        Name = ResourceHelper.GetString(Strings.Commands.CreationDate, Context.Interaction.UserLocale),
                        Value = user.CreatedAt.ToString("dd/MM/yy")
                    },
                    new EmbedFieldProperties
                    {
                        Name = ResourceHelper.GetString(Strings.Commands.UserId, Context.Interaction.UserLocale),
                        Value = $"`{user.Id}`"
                    }
                }
            };

            if(user.PrimaryGuild?.Tag is not null && user.PrimaryGuild.HasBadge)
            {
                string? badgeUrl = user.PrimaryGuild.GetBadgeUrl(ImageFormat.Png)?.ToString();

                var embedAuthor = new EmbedAuthorProperties {
                    Name = user.PrimaryGuild.Tag,
                };
                
                if (badgeUrl is not null)
                    embedAuthor.IconUrl = badgeUrl;

                embed.Author = embedAuthor;
            }

            if(bannerUrl is not null)
                embed.Image = $"{bannerUrl}{SIZE_URL_PARAM}";

            if(avatarUrl is not null)
                embed.Thumbnail = new EmbedThumbnailProperties($"{avatarUrl}{SIZE_URL_PARAM}");

            await Context.Interaction.SendFollowupMessageAsync(
                new InteractionMessageProperties
                {
                    Embeds = new[] { embed }
                }
            );
        }
        catch (SlashCommandException)
        {
            throw;
        }
    }
}