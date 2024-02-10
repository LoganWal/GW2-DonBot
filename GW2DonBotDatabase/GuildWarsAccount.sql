CREATE TABLE [dbo].[GuildWarsAccount](
	[GuildWarsAccountId] [uniqueidentifier] NOT NULL,
	[DiscordId] [bigint] NOT NULL,
	[GuildWarsApiKey] [nvarchar](1000) NOT NULL,
	[GuildWarsAccountName] [nvarchar](1000) NOT NULL,
	[GuildWarsGuilds] [nvarchar](1000) NULL,
	[World] [int] NOT NULL,
	[FailedApiPullCount] [int] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[GuildWarsAccountId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
ALTER TABLE [dbo].[GuildWarsAccount] ADD  DEFAULT ((0)) FOR [FailedApiPullCount]
GO