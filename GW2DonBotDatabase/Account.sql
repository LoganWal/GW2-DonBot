CREATE TABLE [dbo].[Account]
(
	[DiscordId] BIGINT NOT NULL PRIMARY KEY, 
    [GW2AccountId] NCHAR(1000) NULL,
    [GW2AccountName] NCHAR(1000) NULL, 
    [Gw2ApiKey] NCHAR(1000) NULL
)
