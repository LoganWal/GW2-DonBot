CREATE TABLE [dbo].[Raffle](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Description] [nvarchar](4000) NULL,
	[IsActive] [bit] NOT NULL,
	[GuildId] [bigint] NOT NULL,
	[RaffleType] [int] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
ALTER TABLE [dbo].[Raffle] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[Raffle] ADD  DEFAULT ((0)) FOR [RaffleType]
GO
ALTER TABLE [dbo].[Raffle] ADD  CONSTRAINT [FK_Raffle_GuildId] FOREIGN KEY([GuildId])
REFERENCES [dbo].[Guild] ([GuildId])
GO
ALTER TABLE [dbo].[Raffle] CHECK CONSTRAINT [FK_Raffle_GuildId]
GO