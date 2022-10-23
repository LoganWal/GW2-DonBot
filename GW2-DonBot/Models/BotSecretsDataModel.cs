using Newtonsoft.Json.Linq;

namespace Models
{
    using Newtonsoft.Json;

    public class BotSecretsDataModel
    {
        public string? BotToken { get; set; }

        public string? PvECommandPassword { get; set; }

        public string? PvEDebugPostChannelId { get; set; }

        public string? PvEDebugUploadChannelId { get; set; }

        public string? PvEDebugWebhookUrl { get; set; }

        public string? PvEGuildId { get; set; }

        public string? PvEPostChannelId { get; set; }

        public string? PvEUploadChannelId { get; set; }

        public string? PvEWebhookUrl { get; set; }

        public string? WvWAllianceMemberRoleId { get; set; }

        public string? WvWCommandPassword { get; set; }

        public string? WvWDebugPostChannelId { get; set; }

        public string? WvWDebugUploadChannelId { get; set; }

        public string? WvWDebugWebhookUrl { get; set; }

        public string? WvWGuildId { get; set; }

        public string? WvWMemberRoleId { get; set; }

        public string? WvWPostChannelId { get; set; }

        public string? WvWPrimaryGuildId { get; set; }

        public string? WvWSecondaryGuildIds { get; set; }

        public string? WvWUploadChannelId { get; set; }

        public string? WvWVerifiedRoleId { get; set; }

        public string? WvWWebhookUrl { get; set; }

        public string? SqlServerConnection { get; set; }
    }
}
