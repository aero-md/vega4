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
        var downloadTasks = emotes.Select(e => client.GetByteArrayAsync(e.Url).ContinueWith(t => Tuple.Create(t.Result, e))).ToList();

        List<Tuple<byte[], CustomEmote>> emoteDataBytes = (await Task.WhenAll(downloadTasks)).ToList();

        // Create Zip in memory
        using var memoryStream = new MemoryStream();
        using (var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            for (int i = 0; i < emoteDataBytes.Count; i++)
            {
                var bytes = emoteDataBytes[i].Item1;
                var emoteInfo = emoteDataBytes[i].Item2;
                var zipEntry = zipArchive.CreateEntry(string.Format(EMOTE_FILE_NAME_FORMAT, i + 1, emoteInfo.Animated ? "gif" : "png"));
                using var entryStream = zipEntry.Open();
                await entryStream.WriteAsync(bytes);
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
            var emote = match.Value;
            var cleanupRegex = new Regex("[<,>]", RegexOptions.ECMAScript);
            emote = cleanupRegex.Replace(emote, "");

            var parts = emote.Split(':').Where(x => x != "").ToList();

            //<:huh:1293177600025301062>

            bool isAnimated = parts.Count > 2;
            string name = parts[0];
            string id = parts[1];
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