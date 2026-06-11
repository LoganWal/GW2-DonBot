using System.ComponentModel.DataAnnotations;

namespace DonBot.Models.Entities;

public class ScheduledEvent
{
    [Key]
    public long ScheduledEventId { get; init; }

    public long GuildId { get; set; }

    [MaxLength(256)]
    public string Message { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string ResponseOptionsJson { get; set; } = string.Empty;

    public long ChannelId { get; set; }

    public short Day { get; set; }

    public short Hour { get; set; }

    public long? MessageId { get; set; }

    public DateTime UtcEventTime { get; set; }

    public short EventType { get; set; }

    public short RepeatIntervalDays { get; set; } = 7;
}
