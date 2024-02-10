CREATE TABLE [dbo].[Guild](
	[GuildId] [bigint] NOT NULL,
	[LogDropOffChannelId] [bigint] NULL,
	[DiscordGuildMemberRoleId] [bigint] NULL,
	[DiscordSecondaryMemberRoleId] [bigint] NULL,
	[DiscordVerifiedRoleId] [bigint] NULL,
	[Gw2GuildMemberRoleId] [nvarchar](128) NULL,
	[Gw2SecondaryMemberRoleIds] [nvarchar](1000) NULL,
	[PlayerReportChannelId] [bigint] NULL,
	[WvwPlayerActivityReportChannelId] [bigint] NULL,
	[AnnouncementChannelId] [bigint] NULL,
	[LogReportChannelId] [bigint] NULL,
	[AdvanceLogReportChannelId] [bigint] NULL,
	[StreamLogChannelId] [bigint] NULL,
PRIMARY KEY CLUSTERED 
(
	[GuildId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO