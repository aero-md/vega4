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

namespace MessageCommands;

public class DownloadEmotes : ApplicationCommandModule<ApplicationCommandContext>
{
    public const int MAX_EMOTES = 20;
    public const string EMOTE_FILE_NAME_FORMAT = "emote{0}.{1}";
    public const string EMOTE_ZIPFILE_NAME = "emotes.zip";

    [DefferedResponse]
    [RequireUserPermissions<ApplicationCommandContext>(Permissions.AttachFiles)]
    [RequireBotPermissions<ApplicationCommandContext>(Permissions.AttachFiles)]
    [MessageCommand("DownloadEmotes")]
    public async Task Execute(RestMessage message) {
        var msgRef = message.MessageSnapshots.FirstOrDefault() ?? null;
        string input = msgRef?.Message.Content ?? message.Content;
        
        List<CustomEmote> emotes = ParseEmotes(input);

        // Business validations
        if (emotes.Count == 0)
            throw new SlashCommandBusinessException(Strings.Exceptions.NoEmoteInMessage);
        if (emotes.Count > MAX_EMOTES)
            throw new SlashCommandBusinessException(Strings.Exceptions.TooManyEmotesInMessage);

        using HttpClient client = new HttpClient();
        // Download all PNGs concurrently
        var downloadTasks = new List<Tuple<Task<byte[]>, CustomEmote>>();
        foreach (var emote in emotes)
        {
            downloadTasks.Add(new Tuple<Task<byte[]>, CustomEmote>(client.GetByteArrayAsync(emote.Url), emote));
        }

        // Await all downloads to complete
        var emoteDataBytes = await Task.WhenAll(
            downloadTasks.Select(async tuple => (DataBytes: await tuple.Item1, EmoteInfo: tuple.Item2))
        );

        // Create Zip in memory
        using var memoryStream = new MemoryStream();
        using (var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            // For each downloaded emote, create a new entry in the zip file
            foreach (var tuple in emoteDataBytes)
            {
                var (dataBytes, emoteInfo) = tuple;
                var zipEntry = zipArchive.CreateEntry(emoteInfo.Filename);
                using var entryStream = zipEntry.Open();
                await entryStream.WriteAsync(dataBytes);
            }
        }
        memoryStream.Seek(0, SeekOrigin.Begin);

        await Context.Interaction.SendFollowupMessageAsync(
            new InteractionMessageProperties
            {
                Content = ResourceHelper.GetString(
                    emotes.Count > 1 ? Strings.Commands.DlEmotesResultMultiple : Strings.Commands.DlEmotesResultSingle,
                    Context.Interaction.UserLocale,
                    emotes.Count
                ),

                Attachments = new[]
                {
                    new AttachmentProperties(EMOTE_ZIPFILE_NAME, memoryStream)
                }
            }
        );
    }

    private static List<CustomEmote> ParseEmotes(string msgContent) {
        Regex findEmotesRegex = new Regex("<(a)?:(.*?):(.*?)>", RegexOptions.ECMAScript);
        MatchCollection matches = findEmotesRegex.Matches(msgContent);

        List<CustomEmote> emoteList = new();

        foreach (Match match in matches)
        {
            bool isAnimated = match.Groups[1].Success;
            string name = match.Groups[2].Value;
            string id = match.Groups[3].Value;
            string filename = string.Format("{0}.{1}", name, isAnimated ? "gif" : "png");
            string url = string.Format("https://cdn.discordapp.com/emojis/{0}.{1}?size=512&quality=lossless", id, isAnimated ? "gif" : "png");

            // If emote already in list, don't add it to list
            if (!emoteList.Exists(y => y.Id == id))
            {
                emoteList.Add(new CustomEmote(isAnimated, id, name, filename, url));
            }
        }
        return emoteList;
        
    }
}