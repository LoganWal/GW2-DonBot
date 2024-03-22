CREATE TABLE [dbo].[PlayerFightLog]
(
    [PlayerFightLogId] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [FightLogId] BIGINT NOT NULL,
    [GuildWarsAccountName] NVARCHAR(1000) NOT NULL,
    [Damage] BIGINT NOT NULL,
    [QuicknessDuration] DECIMAL(6,3) NOT NULL,
    [AlacDuration] DECIMAL(6,3) NOT NULL
    FOREIGN KEY (FightLogId) REFERENCES FightLog(FightLogId)
);