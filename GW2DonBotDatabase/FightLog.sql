CREATE TABLE [dbo].[FightLog]
(
    [FightLogId] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [GuildId] BIGINT NOT NULL,
    [Url] NVARCHAR(2000) NOT NULL,
    [FightType] SMALLINT NOT NULL,
    [FightStart] DateTime2 NOT NULL,
    [FightDurationInMs] BIGINT NOT NULL,
    [IsSuccess] BIT NOT NULL,
    FOREIGN KEY (GuildId) REFERENCES Guild(GuildId)
);