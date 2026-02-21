using System.ComponentModel.DataAnnotations;

namespace DonBot.Models.Entities;

public class Guild
{
    [Key]
    public long GuildId { get; init; }

    public long? LogDropOffChannelId { get; set; }

    public long? DiscordGuildMemberRoleId { get; set; }

    public long? DiscordSecondaryMemberRoleId { get; set; }

    public long? DiscordVerifiedRoleId { get; set; }

    [MaxLength(128)]
    public string? Gw2GuildMemberRoleId { get; set; }

    [MaxLength(1000)]
    public string? Gw2SecondaryMemberRoleIds { get; set; }

    public long? PlayerReportChannelId { get; set; }

    public long? WvwPlayerActivityReportChannelId { get; set; }

    public long? AnnouncementChannelId { get; set; }

    public long? LogReportChannelId { get; set; }

    public long? AdvanceLogReportChannelId { get; set; }

    public long? StreamLogChannelId { get; set; }

    public bool RaidAlertEnabled { get; set; }

    public long? RaidAlertChannelId { get; set; }

    public bool RemoveSpamEnabled { get; set; }

    public long? RemovedMessageChannelId { get; set; }
}