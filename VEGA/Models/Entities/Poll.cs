using System.ComponentModel.DataAnnotations;

namespace Models.Entities;

public class Poll
{
    [Key]
    public Guid PollId { get; set; }

    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Id of the embed message holding the vote buttons. Set after the message is posted.
    /// </summary>
    public ulong MessageId { get; set; }

    public ulong CreatorId { get; set; }

    [Required, MaxLength(2000)]
    public string Question { get; set; } = "";

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Button labels in display order. For yes/no polls these are pre-localized at creation
    /// time so result rendering doesn't depend on the viewer's locale.
    /// </summary>
    public string[] Options { get; set; } = Array.Empty<string>();

    public DateTime EndAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsCompleted { get; set; } = false;

    /// <summary>
    /// Locale of the poll creator, used to localize the result embed when the timer fires.
    /// </summary>
    [MaxLength(10)]
    public string Locale { get; set; } = "en-US";

    private Poll() { }

    public Poll(
        ulong guildId,
        ulong channelId,
        ulong creatorId,
        string question,
        string? imageUrl,
        string[] options,
        DateTime endAt,
        string locale,
        DateTime? createdAt = null
    )
    {
        GuildId = guildId;
        ChannelId = channelId;
        CreatorId = creatorId;
        Question = question;
        ImageUrl = imageUrl;
        Options = options;
        EndAt = endAt;
        Locale = locale;
        CreatedAt = createdAt ?? DateTime.UtcNow;
    }
}
