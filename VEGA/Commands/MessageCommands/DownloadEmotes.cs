using static Core.GlobalRegistry;
using System.Text.RegularExpressions;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Models;
using System.IO.Compression;
using NetCord;
using NetCord.Services;
using Exceptions;
using Resources;
using Core.CustomCommandAttributes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace MessageCommands;

public class DownloadEmotes : ApplicationCommandModule<ApplicationCommandContext>
{
    public const int MAX_EMOTES = 20;
    public const string EMOTE_ZIPFILE_NAME = "emotes.zip";

    // Custom-id prefixes for the widget buttons. The trailing widgetId is appended
    // with CUSTOMID_SEPARATOR; NetCord's ComponentInteractionService matches the
    // prefix and binds the remaining segment as a string parameter.
    public const string CUSTOMID_ZIP_PREFIX = "dl_emotes:zip";
    public const string CUSTOMID_ADD_PREFIX = "dl_emotes:add";
    public const char CUSTOMID_SEPARATOR = ':';

    public const string CACHE_KEY_PREFIX = "dl_emotes:";
    private const int WIDGET_CACHE_TTL_MINUTES = 10;

    [DefferedResponse]
    [RequireUserPermissions<ApplicationCommandContext>(Permissions.AttachFiles)]
    [RequireBotPermissions<ApplicationCommandContext>(Permissions.AttachFiles)]
    [MessageCommand("DownloadEmotes")]
    public async Task Execute(RestMessage message)
    {
        var msgRef = message.MessageSnapshots.FirstOrDefault() ?? null;
        string input = msgRef?.Message.Content ?? message.Content;

        List<CustomEmote> emotes = ParseEmotes(input);

        if (emotes.Count == 0)
            throw new SlashCommandBusinessException(Strings.Exceptions.NoEmoteInMessage);
        if (emotes.Count > MAX_EMOTES)
            throw new SlashCommandBusinessException(Strings.Exceptions.TooManyEmotesInMessage);

        // Cache the emote list under a unique key — the buttons reference this key in their customId
        var widgetId = Guid.NewGuid().ToString("N");
        var cache = MainServiceProvider.GetRequiredService<IMemoryCache>();
        cache.Set(
            CACHE_KEY_PREFIX + widgetId,
            new EmoteWidgetState(emotes, Context.Interaction.User.Id),
            TimeSpan.FromMinutes(WIDGET_CACHE_TTL_MINUTES)
        );

        var locale = Context.Interaction.UserLocale;
        await Context.Interaction.SendFollowupMessageAsync(
            new InteractionMessageProperties
            {
                Content = ResourceHelper.GetString(
                    emotes.Count > 1 ? Strings.Commands.DlEmotesWidgetMultiple : Strings.Commands.DlEmotesWidgetSingle,
                    locale,
                    emotes.Count
                ),
                Components = new IMessageComponentProperties[]
                {
                    new ActionRowProperties(new IActionRowComponentProperties[]
                    {
                        new ButtonProperties(
                            $"{CUSTOMID_ZIP_PREFIX}{CUSTOMID_SEPARATOR}{widgetId}",
                            ResourceHelper.GetString(Strings.Commands.DlEmotesBtnZip, locale),
                            ButtonStyle.Primary
                        ),
                        new ButtonProperties(
                            $"{CUSTOMID_ADD_PREFIX}{CUSTOMID_SEPARATOR}{widgetId}",
                            ResourceHelper.GetString(Strings.Commands.DlEmotesBtnAdd, locale),
                            ButtonStyle.Success
                        )
                    })
                }
            }
        );
    }

    // Discord emote tag: <:name:snowflake> or <a:name:snowflake>.
    // Name is 2–32 word chars; snowflake is 17–20 digits.
    // Tight quantifiers prevent both URL injection (id was previously .*?)
    // and pathological matches on long messages.
    private static readonly Regex EmoteTagRegex = new(
        @"<(a)?:(\w{2,32}):(\d{17,20})>",
        RegexOptions.ECMAScript | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100)
    );

    public static List<CustomEmote> ParseEmotes(string msgContent)
    {
        MatchCollection matches = EmoteTagRegex.Matches(msgContent);

        List<CustomEmote> emoteList = new();

        foreach (Match match in matches)
        {
            bool isAnimated = match.Groups[1].Success;
            string name = match.Groups[2].Value;
            string id = match.Groups[3].Value;

            // Defense in depth: the regex already enforces digits, but a future
            // loosening shouldn't silently leak unparseable ids into the URL.
            if (!ulong.TryParse(id, out _))
                continue;

            string filename = string.Format("{0}.{1}", name, isAnimated ? "gif" : "png");
            string url = string.Format("https://cdn.discordapp.com/emojis/{0}.{1}?size=512&quality=lossless", id, isAnimated ? "gif" : "png");

            if (!emoteList.Exists(y => y.Id == id))
            {
                emoteList.Add(new CustomEmote(isAnimated, id, name, filename, url));
            }
        }
        return emoteList;
    }

    /// <summary>
    /// Downloads all emotes from Discord CDN concurrently and returns the (bytes, info) pairs.
    /// </summary>
    public static async Task<(byte[] DataBytes, CustomEmote EmoteInfo)[]> DownloadEmotesAsync(
        IEnumerable<CustomEmote> emotes,
        HttpClient client
    )
    {
        var tasks = emotes.Select(async e => (DataBytes: await client.GetByteArrayAsync(e.Url), EmoteInfo: e));
        return await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Builds a ZIP archive in memory containing all emote files.
    /// Caller is responsible for disposing the returned MemoryStream.
    /// </summary>
    public static async Task<MemoryStream> BuildZipAsync((byte[] DataBytes, CustomEmote EmoteInfo)[] downloaded)
    {
        var memoryStream = new MemoryStream();
        using (var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (dataBytes, emoteInfo) in downloaded)
            {
                var zipEntry = zipArchive.CreateEntry(emoteInfo.Filename);
                using var entryStream = zipEntry.Open();
                await entryStream.WriteAsync(dataBytes);
            }
        }
        memoryStream.Seek(0, SeekOrigin.Begin);
        return memoryStream;
    }

    /// <summary>
    /// Resolves a widgetId back to its cached state. Throws a business exception
    /// if the cache entry has expired (TTL elapsed since the widget was posted).
    /// </summary>
    public static EmoteWidgetState LoadWidgetState(string widgetId)
    {
        var cache = MainServiceProvider.GetRequiredService<IMemoryCache>();
        if (!cache.TryGetValue(CACHE_KEY_PREFIX + widgetId, out EmoteWidgetState? state) || state == null)
            throw new SlashCommandBusinessException(Strings.Exceptions.DlEmotesWidgetExpired);

        return state;
    }

    /// <summary>
    /// Rejects clicks coming from a user other than the one who created the widget.
    /// </summary>
    public static void EnsureInvoker(MessageComponentInteraction interaction, EmoteWidgetState state)
    {
        if (interaction.User.Id != state.InvokerId)
            throw new SlashCommandBusinessException(Strings.Exceptions.DlEmotesNotInvoker);
    }

    /// <summary>
    /// Uploads each emote as a guild emoji via Discord REST.
    /// Returns (createdCount, failures) where failures is a list of "name: reason".
    /// </summary>
    public static async Task<(int Created, List<string> Failures)> AddEmotesToGuildAsync(
        ulong guildId,
        (byte[] DataBytes, CustomEmote EmoteInfo)[] downloaded,
        RestClient restClient
    )
    {
        int created = 0;
        var failures = new List<string>();

        foreach (var (dataBytes, emoteInfo) in downloaded)
        {
            try
            {
                var format = emoteInfo.Animated ? ImageFormat.Gif : ImageFormat.Png;
                var props = new GuildEmojiProperties(emoteInfo.Name, new ImageProperties(format, dataBytes, false));
                await restClient.CreateGuildEmojiAsync(guildId, props);
                created++;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to add emote {EmoteName} ({EmoteId}) to guild {GuildId}",
                    emoteInfo.Name, emoteInfo.Id, guildId);
                failures.Add($"`{emoteInfo.Name}` ({ex.Message})");
            }
        }

        return (created, failures);
    }
}

/// <summary>
/// State stored in IMemoryCache between widget creation and button click.
/// </summary>
public record EmoteWidgetState(List<CustomEmote> Emotes, ulong InvokerId);
