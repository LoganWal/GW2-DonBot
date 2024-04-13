using System.ComponentModel.DataAnnotations;

namespace Models.Entities
{
    public class Guild
    {
        [Key]
        public long GuildId { get; set; }

        public long? LogDropOffChannelId { get; set; }

        public long? DiscordGuildMemberRoleId { get; set; }

        public long? DiscordSecondaryMemberRoleId { get; set; }

        public long? DiscordVerifiedRoleId { get; set; }

        public string? Gw2GuildMemberRoleId { get; set; }

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
    }
}