CREATE TABLE [dbo].[FightLog](
	[FightLogId] [bigint] IDENTITY(1,1) NOT NULL,
	[GuildId] [bigint] NOT NULL,
	[Url] [nvarchar](2000) NOT NULL,
	[FightType] [smallint] NOT NULL,
	[FightStart] [datetime2](7) NOT NULL,
	[FightDurationInMs] [bigint] NOT NULL,
	[IsSuccess] [bit] NOT NULL,
	[FightPercent] [decimal](6, 2) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[FightLogId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
ALTER TABLE [dbo].[FightLog] ADD  DEFAULT ((0)) FOR [FightPercent]
GO
ALTER TABLE [dbo].[FightLog] ADD FOREIGN KEY([GuildId])
REFERENCES [dbo].[Guild] ([GuildId])