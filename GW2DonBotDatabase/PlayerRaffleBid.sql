CREATE TABLE [dbo].[PlayerRaffleBid](
	[RaffleId] [int] NOT NULL,
	[DiscordId] [bigint] NOT NULL,
	[PointsSpent] [decimal](16, 3) NOT NULL,
 CONSTRAINT [PK_PlayerRaffleBid] PRIMARY KEY CLUSTERED 
(
	[RaffleId] ASC,
	[DiscordId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
ALTER TABLE [dbo].[PlayerRaffleBid]  WITH CHECK ADD FOREIGN KEY([DiscordId])
REFERENCES [dbo].[Account] ([DiscordId])
GO
ALTER TABLE [dbo].[PlayerRaffleBid]  WITH CHECK ADD FOREIGN KEY([RaffleId])
REFERENCES [dbo].[Raffle] ([Id])
GO
