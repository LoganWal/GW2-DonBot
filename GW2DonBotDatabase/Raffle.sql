CREATE TABLE [dbo].[Raffle]
(
	[Id] INT IDENTITY(1,1) PRIMARY KEY,
    [Description] NVARCHAR(4000) NOT NULL,
	[IsActive] BIT NOT NULL DEFAULT 1,
	[GuildId] bigint NOT NULL
	CONSTRAINT FK_Raffle_GuildId_Guild_GuildId FOREIGN KEY (GuildId) REFERENCES Guild(GuildId)
)
