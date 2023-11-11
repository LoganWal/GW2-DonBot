CREATE TABLE [dbo].[Guild](
	[GuildId] [bigint] NOT NULL,
	[CommandPassword] [nvarchar](256) NULL,
	[WebhookChannelId] [bigint] NULL,
	[PostChannelId] [bigint] NULL,
	[Webhook] [nvarchar](256) NULL,
	[DebugWebhookChannelId] [bigint] NULL,
	[DebugPostChannelId] [bigint] NULL,
	[DebugWebhook] [nvarchar](256) NULL,
	[DiscordGuildMemberRoleId] [bigint] NULL,
	[DiscordSecondaryMemberRoleId] [bigint] NULL,
	[DiscordVerifiedRoleId] [bigint] NULL,
	[Gw2GuildMemberRoleId] [nvarchar](128) NULL,
	[Gw2SecondaryMemberRoleIds] [nvarchar](1000) NULL,
	[PlayerReportWebhook] [nvarchar](256) NULL,
	[AdminPlayerReportWebhook] [nvarchar](256) NULL,
	[AnnouncementWebhook] [nvarchar](256) NULL,
	[AdminAdvancePlayerReportWebhook] [nvarchar](256) NULL,
	[StreamLogsWebhook] [nvarchar](256) NULL,
	[PlayerReportChannelId] [bigint] NULL,
	[WvwPlayerActivityReportWebhook] [nvarchar](256) NULL,
	[WvwPlayerActivityReportChannelId] [bigint] NULL
) ON [PRIMARY]
GO
ALTER TABLE [dbo].[Guild] ADD PRIMARY KEY CLUSTERED 
(
	[GuildId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF) ON [PRIMARY]
GO
