CREATE TABLE [dbo].[Account](
	[DiscordId] [bigint] NOT NULL,
	[Points] [decimal](16, 3) NOT NULL,
	[PreviousPoints] [decimal](16, 3) NOT NULL,
	[AvailablePoints] [decimal](16, 3) NOT NULL,
	[LastWvwLogDateTime] [datetime] NULL,
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
