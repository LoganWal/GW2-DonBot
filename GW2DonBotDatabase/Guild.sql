CREATE TABLE [dbo].[Guild]
(
	[GuildId] BIGINT NOT NULL PRIMARY KEY, 
    [CommandPassword] NVARCHAR(256) NULL,
    [WebhookChannelId] BIGINT NULL, 
    [PostChannelId] BIGINT NULL,
    [Webhook] NVARCHAR(256) NULL,
    [DebugWebhookChannelId] BIGINT NULL, 
    [DebugPostChannelId] BIGINT NULL,
    [DebugWebhook] NVARCHAR(256) NULL,
    [DiscordGuildMemberRoleId] BIGINT NULL,
    [DiscordSecondaryMemberRoleId] BIGINT NULL,
    [DiscordVerifiedRoleId] BIGINT NULL,
    [Gw2GuildMemberRoleId] NVARCHAR(128) NULL,
    [Gw2SecondaryMemberRoleIds] NVARCHAR(1000) NULL, 
    [AnnouncementWebhook] NVARCHAR(256) NULL
)