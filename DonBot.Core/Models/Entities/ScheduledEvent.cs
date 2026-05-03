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

    public short Day { get; set; }

    public short Hour { get; init; }

    public long? MessageId { get; set; }

    public DateTime UtcEventTime { get; set; }

    public short EventType { get; init; }

    public long? RoleId { get; init; }

    public short RepeatIntervalDays { get; init; } = 7;
}