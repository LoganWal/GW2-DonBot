CREATE TABLE [dbo].[Raffle](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Description] [nvarchar](4000) NOT NULL,
	[IsActive] [bit] NOT NULL,
	[GuildId] [bigint] NOT NULL,
	[RaffleType] [int] NOT NULL
) ON [PRIMARY]
GO
ALTER TABLE [dbo].[Raffle] ADD PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF) ON [PRIMARY]
GO
ALTER TABLE [dbo].[Raffle] ADD  DEFAULT (1) FOR [IsActive]
GO
ALTER TABLE [dbo].[Raffle] ADD  DEFAULT (0) FOR [RaffleType]
GO
ALTER TABLE [dbo].[Raffle]  WITH CHECK ADD CONSTRAINT [FK_Raffle_GuildId] FOREIGN KEY([GuildId])
REFERENCES [dbo].[Guild] ([GuildId])
GO
ALTER TABLE [dbo].[Raffle] CHECK CONSTRAINT [FK_Raffle_GuildId]
GO
