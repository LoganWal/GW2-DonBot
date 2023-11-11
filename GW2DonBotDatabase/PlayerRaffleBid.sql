CREATE TABLE [dbo].[PlayerRaffleBid](
	[RaffleId] [int] NOT NULL,
	[DiscordId] [bigint] NOT NULL,
	[PointsSpent] [decimal](16, 3) NOT NULL
) ON [PRIMARY]
GO
ALTER TABLE [dbo].[PlayerRaffleBid] ADD  CONSTRAINT [PK_PlayerRaffleBid] PRIMARY KEY CLUSTERED 
(
	[RaffleId] ASC,
	[DiscordId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF) ON [PRIMARY]
GO
ALTER TABLE [dbo].[PlayerRaffleBid]  WITH CHECK ADD FOREIGN KEY([DiscordId])
REFERENCES [dbo].[Account] ([DiscordId])
GO
ALTER TABLE [dbo].[PlayerRaffleBid]  WITH CHECK ADD FOREIGN KEY([RaffleId])
REFERENCES [dbo].[Raffle] ([Id])
GO