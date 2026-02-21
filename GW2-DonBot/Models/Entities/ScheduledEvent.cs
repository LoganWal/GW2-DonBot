using System.ComponentModel.DataAnnotations;

namespace DonBot.Models.Entities;

public class ScheduledEvent
{
    [Key]
    public long ScheduledEventId { get; init; }

    public long GuildId { get; init; }

    [MaxLength(256)]
    public string Message { get; init; } = string.Empty;

    public long ChannelId { get; init; }

    public short Day { get; init; }

    public short Hour { get; init; }

    public long? MessageId { get; set; }

    public DateTime UtcEventTime { get; set; }
}