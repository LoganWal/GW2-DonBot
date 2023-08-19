using System.ComponentModel.DataAnnotations;

namespace Models.Entities
{
    public class Guild
    {
        [Key]
        public long GuildId { get; set; }

        public string? CommandPassword { get; set; }

        public long? WebhookChannelId { get; set; }

        public long? PostChannelId { get; set; }

        public string? Webhook { get; set; }

        public string? PlayerReportWebhook { get; set; }

        public string? AdminPlayerReportWebhook { get; set; }

        public string? AdminAdvancePlayerReportWebhook { get; set; }

        public long? DebugWebhookChannelId { get; set; }

        public long? DebugPostChannelId { get; set; }

        public string? DebugWebhook { get; set; }

        public long? DiscordGuildMemberRoleId { get; set; }

        public long? DiscordSecondaryMemberRoleId { get; set; }

        public long? DiscordVerifiedRoleId { get; set; }

        public string? Gw2GuildMemberRoleId { get; set; }

        public string? Gw2SecondaryMemberRoleIds { get; set; }

        public string? AnnouncementWebhook { get; set; }
    }
}