CREATE TABLE [dbo].[PlayerFightLog](
	[PlayerFightLogId] [bigint] IDENTITY(1,1) NOT NULL,
	[FightLogId] [bigint] NOT NULL,
	[GuildWarsAccountName] [nvarchar](1000) NOT NULL,
	[Damage] [bigint] NOT NULL,
	[QuicknessDuration] [decimal](6, 2) NULL,
	[AlacDuration] [decimal](6, 2) NULL,
	[SubGroup] [bigint] NOT NULL,
	[DamageDownContribution] [bigint] NOT NULL,
	[Cleanses] [bigint] NOT NULL,
	[Strips] [bigint] NOT NULL,
	[StabGenerated] [decimal](6, 2) NOT NULL,
	[Healing] [bigint] NOT NULL,
	[BarrierGenerated] [bigint] NOT NULL,
	[DistanceFromTag] [decimal](16, 2) NOT NULL,
	[TimesDowned] [int] NOT NULL,
	[Interrupts] [bigint] NOT NULL,
	[NumberOfHitsWhileBlinded] [bigint] NOT NULL,
	[NumberOfMissesAgainst] [bigint] NOT NULL,
	[NumberOfTimesBlockedAttack] [bigint] NOT NULL,
	[NumberOfTimesEnemyBlockedAttack] [bigint] NOT NULL,
	[NumberOfBoonsRipped] [bigint] NOT NULL,
	[DamageTaken] [bigint] NOT NULL,
	[BarrierMitigation] [bigint] NOT NULL,
	[CerusOrbsCollected] [bigint] NOT NULL,
	[Kills] [bigint] NOT NULL,
	[Deaths] [bigint] NOT NULL,
	[Downs] [bigint] NOT NULL,
	[DeimosOilsTriggered] [bigint] NOT NULL,
	[CerusSpreadHitCount] [bigint] NOT NULL,
	[TimesInterrupted] [bigint] NOT NULL,
	[CerusPhaseOneDamage] [decimal](10, 3) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[PlayerFightLogId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
ALTER TABLE [dbo].[PlayerFightLog] ADD  DEFAULT ((0)) FOR [SubGroup]
GO
ALTER TABLE [dbo].[PlayerFightLog] ADD  DEFAULT ((0)) FOR [DamageDownContribution]
GO
ALTER TABLE [dbo].[PlayerFightLog] ADD  DEFAULT ((0)) FOR [Cleanses]
GO
ALTER TABLE [dbo].[PlayerFightLog] ADD  DEFAULT ((0)) FOR [Strips]
GO
ALTER TABLE [dbo].[PlayerFightLog] ADD  DEFAULT ((0)) FOR [StabGenerated]
GO
ALTER TABLE [dbo].[PlayerFightLog] ADD  DEFAULT ((0)) FOR [Healing]
GO
ALTER TABLE [dbo].[PlayerFightLog] ADD  DEFAULT ((0)) FOR [BarrierGenerated]
GO
ALTER TABLE [dbo].[PlayerFightLog] ADD  DEFAULT ((0)) FOR [DistanceFromTag]
GO
ALTER TABLE [dbo].[PlayerFightLog] ADD  DEFAULT ((0)) FOR [TimesDowned]
GO
ALTER TABLE [dbo].[PlayerFightLog] ADD  DEFAULT ((0)) FOR [Interrupts]
GO
ALTER TABLE [dbo].[PlayerFightLog] ADD  DEFAULT ((0)) FOR [NumberOfHitsWhileBlinded]
GO
ALTER TABLE [dbo].[PlayerFightLog] ADD  DEFAULT ((0)) FOR [NumberOfMissesAgainst]
GO
ALTER TABLE [dbo].[PlayerFightLog] ADD  DEFAULT ((0)) FOR [NumberOfTimesBlockedAttack]
GO
ALTER TABLE [dbo].[PlayerFightLog] ADD  DEFAULT ((0)) FOR [NumberOfTimesEnemyBlockedAttack]
GO
ALTER TABLE [dbo].[PlayerFightLog] ADD  DEFAULT ((0)) FOR [NumberOfBoonsRipped]
GO
ALTER TABLE [dbo].[PlayerFightLog] ADD  DEFAULT ((0)) FOR [DamageTaken]
GO
ALTER TABLE [dbo].[PlayerFightLog] ADD  DEFAULT ((0)) FOR [BarrierMitigation]
GO
ALTER TABLE [dbo].[PlayerFightLog] ADD  DEFAULT ((0)) FOR [CerusOrbsCollected]
GO
ALTER TABLE [dbo].[PlayerFightLog] ADD  DEFAULT ((0)) FOR [Kills]
GO
ALTER TABLE [dbo].[PlayerFightLog] ADD  DEFAULT ((0)) FOR [Deaths]
GO
ALTER TABLE [dbo].[PlayerFightLog] ADD  DEFAULT ((0)) FOR [Downs]
GO
ALTER TABLE [dbo].[PlayerFightLog] ADD  DEFAULT ((0)) FOR [DeimosOilsTriggered]
GO
ALTER TABLE [dbo].[PlayerFightLog] ADD  DEFAULT ((0)) FOR [CerusSpreadHitCount]
GO
ALTER TABLE [dbo].[PlayerFightLog] ADD  DEFAULT ((0)) FOR [TimesInterrupted]
GO
ALTER TABLE [dbo].[PlayerFightLog] ADD  CONSTRAINT [DF_CerusPhaseOneDamage]  DEFAULT ((0)) FOR [CerusPhaseOneDamage]
GO
ALTER TABLE [dbo].[PlayerFightLog]  WITH CHECK ADD FOREIGN KEY([FightLogId])
REFERENCES [dbo].[FightLog] ([FightLogId])
GO
