CREATE TABLE [dbo].[Account](
	[DiscordId] [bigint] NOT NULL,
	[GW2AccountId] [nvarchar](1000) NULL,
	[GW2AccountName] [nvarchar](1000) NULL,
	[Gw2ApiKey] [nvarchar](1000) NULL,
	[Points] [decimal](16, 3) NOT NULL,
	[PreviousPoints] [decimal](16, 3) NOT NULL,
	[AvailablePoints] [decimal](16, 3) NOT NULL,
	[LastWvwLogDateTime] [datetime] NULL,
	[World] [int] NULL,
	[FailedApiPullCount] [int] NULL,
	[Guilds] [nvarchar](1000) NULL,
PRIMARY KEY CLUSTERED 
(
	[DiscordId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
ALTER TABLE [dbo].[Account] ADD  DEFAULT ((0)) FOR [Points]
GO
ALTER TABLE [dbo].[Account] ADD  DEFAULT ((0)) FOR [PreviousPoints]
GO
ALTER TABLE [dbo].[Account] ADD  DEFAULT ((0)) FOR [AvailablePoints]
GO
