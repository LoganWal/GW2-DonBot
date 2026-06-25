using System.ComponentModel.DataAnnotations;

namespace DonBot.Core.Models.Entities;

public class ScheduledMessageDelete
{
    [Key]
    public long ScheduledMessageDeleteId { get; init; }

    public long ChannelId { get; set; }

    public long MessageId { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime DeleteAfterUtc { get; set; }

    [MaxLength(128)]
    public string Reason { get; set; } = string.Empty;
}
