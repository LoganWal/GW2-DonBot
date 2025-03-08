CREATE TABLE [dbo].[FightsReport]
(
    [FightsReportId] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [GuildId] BIGINT NOT NULL,
    [FightsStart] DateTime2 NOT NULL,
    [FightsEnd] DateTime2 NULL,
    FOREIGN KEY (GuildId) REFERENCES Guild(GuildId)
);