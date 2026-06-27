namespace Models.Entities;

/// <summary>
/// One vote per user per poll: composite PK on (PollId, UserId) enforces uniqueness in DB.
/// </summary>
public class PollVote
{
    public Guid PollId { get; set; }
    public ulong UserId { get; set; }
    public int OptionIndex { get; set; }
    public DateTime VotedAt { get; set; }

    private PollVote() { }

    public PollVote(Guid pollId, ulong userId, int optionIndex, DateTime? votedAt = null)
    {
        PollId = pollId;
        UserId = userId;
        OptionIndex = optionIndex;
        VotedAt = votedAt ?? DateTime.UtcNow;
    }
}
