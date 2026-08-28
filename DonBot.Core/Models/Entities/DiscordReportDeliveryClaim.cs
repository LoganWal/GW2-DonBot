using System.ComponentModel.DataAnnotations;

namespace DonBot.Core.Models.Entities;

public class DiscordReportDeliveryClaim
{
    [Key]
    public long DiscordReportDeliveryClaimId { get; set; }

    public long GuildId { get; set; }

    [MaxLength(64)]
    public string ReportUrlHash { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Source { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
