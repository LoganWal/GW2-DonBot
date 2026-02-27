CREATE TABLE [dbo].[RotationAnomaly](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[AccountName] [nvarchar](100) NOT NULL,
	[CharacterName] [nvarchar](100) NOT NULL,
	[SkillId] [bigint] NOT NULL,
	[SkillName] [nvarchar](200) NOT NULL,
	[ConsecutiveCasts] [int] NOT NULL,
	[AverageInterval] [decimal](10, 3) NOT NULL,
	[MaxDeviation] [decimal](10, 3) NOT NULL,
	[Description] [nvarchar](500) NOT NULL,
	[FightUrl] [nvarchar](500) NOT NULL,
	[DetectedAt] [datetime] NOT NULL,
PRIMARY KEY CLUSTERED
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
ALTER TABLE [dbo].[RotationAnomaly] ADD DEFAULT (getutcdate()) FOR [DetectedAt]
GO
