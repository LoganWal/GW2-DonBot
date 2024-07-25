CREATE TABLE [dbo].[GuildQuote]
(
    [GuildQuoteId] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [GuildId] BIGINT NOT NULL,
    [Quote] [nvarchar](1000) NOT NULL
    FOREIGN KEY (GuildId) REFERENCES Guild(GuildId)
);