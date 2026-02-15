using System.ComponentModel.DataAnnotations;

namespace Models.Entities;

public class Reminder
{
    [Key]
    public Guid ReminderId { get; set; }
    
    public ulong UserId { get; set; }
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    
    public string Message { get; set; } = "";
    
    public DateTime RemindAt { get; set; }
    public DateTime CreatedAt { get; set; }
    
    public bool IsCompleted { get; set; } = false;
    
    private Reminder() {}
    
    public Reminder(
        ulong userId,
        ulong guildId,
        ulong channelId,
        string message,
        DateTime remindAt,
        DateTime? createdAt = null
    )
    {
        UserId = userId;
        GuildId = guildId;
        ChannelId = channelId;
        Message = message;
        RemindAt = remindAt;
        CreatedAt = createdAt ?? DateTime.UtcNow;
        IsCompleted = false;
    }
}
