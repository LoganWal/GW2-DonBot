CREATE TABLE [dbo].[Account]
(
	[DiscordId] BIGINT NOT NULL PRIMARY KEY, 
    [GW2AccountId] NVARCHAR(1000) NULL,
    [GW2AccountName] NVARCHAR(1000) NULL, 
    [Gw2ApiKey] NVARCHAR(1000) NULL,
    [Points] decimal(16, 3)
)
